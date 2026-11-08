using Builder.Declaration;
using SyntaxTree;
using TypeModel.Symbols;
using TypeModel.Types;

namespace Mielia.Passes;

/// <summary>
/// Makes a change to a record <c>for</c> element reach the element. A loop's element is read where the collection
/// keeps it, with no copy, so a member write or a call of a changing routine on the loop variable would change only
/// that read, and releasing an old member value through it would release the element's own value. Two cases:
/// <list type="bullet">
/// <item><b>In place.</b> A loop that walks the places of a memory (<c>MemoryEmitter[T]</c>: <c>List</c>,
/// <c>RotatableList</c>, <c>Memory</c>, <c>Span</c>) over records or tuples, whose body changes the loop variable. The round
/// binds the place the step read (<c>var _lf_place_N = _lf_iter_N.emitted_place()</c>), and every use of the loop
/// variable in the body becomes the record at that place (a <c>RecordAddress</c> projection). A member write and a
/// changing routine act on the element. A use as a value (a new variable, a stored or returned value) copies the
/// record, as a use of a variable does (<see cref="WrapperProjectionExpression.ReadsLoopElement"/>).</item>
/// <item><b>A copy of its own.</b> Any other loop over records that hold managed values (a <c>Set</c>, a <c>Dict</c>
/// walked by entry, an adapter such as <c>where</c>, a generator), whose body changes the loop variable. Changing
/// those elements in place would break the collection (a set element's hash) or the element is not kept anywhere,
/// so the round copies the element into the loop variable (<c>var t = _lf_seen_N</c>), which is torn down at the end
/// of the round. A member write then releases the copy's old value, never the element's.</item>
/// </list>
/// A lambda in an in-place body gets a copy of the element (see <see cref="ElementAtPlace"/>). A loop over the places
/// of a memory falls back to the copy when a name in the body is spelled like the loop variable. A loop whose body only
/// reads the loop variable keeps the read as it is. A loop that takes its element apart (<c>for (a, b) in ...</c>)
/// already binds copies and is left alone.
/// <para>Runs on analyzed user routines before the entity lowering and before scope teardown, so the teardown's
/// member-write release sees the element's place or the copy.</para>
/// </summary>
internal sealed class RecordLoopElementPass(TypeRegistry registry)
{
    private int _count;

    public void Run(Program program)
    {
        for (int i = 0; i < program.Declarations.Count; i++)
        {
            switch (program.Declarations[index: i])
            {
                case RoutineDeclaration r:
                    program.Declarations[index: i] = LowerRoutine(r: r);
                    break;
                case EntityDeclaration e:
                    LowerMembers(members: e.Members);
                    break;
                case RecordDeclaration rec:
                    LowerMembers(members: rec.Members);
                    break;
                case CrashableDeclaration cr:
                    LowerMembers(members: cr.Members);
                    break;
            }
        }
    }

    private void LowerMembers(List<SyntaxTree.Declaration> members)
    {
        for (int i = 0; i < members.Count; i++)
        {
            if (members[index: i] is RoutineDeclaration r)
            {
                members[index: i] = LowerRoutine(r: r);
            }
        }
    }

    internal RoutineDeclaration LowerRoutine(RoutineDeclaration r)
    {
        if (r.Body == null || !IsProgramMieliaFile(file: r.Location.FileName))
        {
            return r;
        }

        Statement body = LowerBody(body: r.Body);
        return ReferenceEquals(objA: body, objB: r.Body)
            ? r
            : r with { Body = body };
    }

    /// <summary>Lowers the body of a recovery variant of a routine written in a Mielia program file.</summary>
    internal Statement LowerVariantBody(Statement body, RoutineInfo routine)
    {
        return IsProgramMieliaFile(file: routine.Location?.FileName ?? body.Location.FileName)
            ? LowerBody(body: body)
            : body;
    }

    private bool IsProgramMieliaFile(string? file)
    {
        return file != null && !registry.IsStandardLibraryFile(filePath: file) &&
               Builder.Frontends.Languages.OfFile(fileName: file) == TypeModel.Enums.Language.Mielia;
    }

    private Statement LowerBody(Statement body)
    {
        return new LoopFinder(pass: this).VisitStatement(stmt: body);
    }

    /// <summary>Finds each lowered <c>for</c> loop (the emitter's declaration followed by the loop over it) in a
    /// block, inner loops first.</summary>
    private sealed class LoopFinder(RecordLoopElementPass pass) : AstRewriter
    {
        protected override Statement VisitBlock(BlockStatement s)
        {
            bool changed = false;
            var statements = new List<Statement>(capacity: s.Statements.Count);
            for (int i = 0; i < s.Statements.Count; i++)
            {
                Statement visited = VisitStatement(stmt: s.Statements[index: i]);
                if (visited is LoopStatement { IsIteratorForLoop: true, IteratorName: { } iterator } loop &&
                    EmitterOf(statements: statements, iterator: iterator) is { } emitter &&
                    pass.LowerLoop(loop: loop, emitter: emitter) is { } lowered)
                {
                    visited = lowered;
                }

                changed |= !ReferenceEquals(objA: visited, objB: s.Statements[index: i]);
                statements.Add(item: visited);
            }

            return changed
                ? s with { Statements = statements }
                : s;
        }

        /// <summary>The type of the emitter a loop walks, from its declaration among the statements before it.</summary>
        private static TypeSymbol? EmitterOf(List<Statement> statements, string iterator)
        {
            for (int i = statements.Count - 1; i >= 0; i--)
            {
                if (statements[index: i] is DeclarationStatement { Declaration: VariableDeclaration vd } &&
                    vd.Name == iterator)
                {
                    return vd.Type?.ResolvedType ?? vd.Initializer?.ResolvedType;
                }
            }

            return null;
        }
    }

    /// <summary>The loop with its element reached in place or copied, or null when it stays as it is.</summary>
    private LoopStatement? LowerLoop(LoopStatement loop, TypeSymbol emitter)
    {
        if (loop.Body is not BlockStatement { Statements: [WhenStatement { IsForStep: true } step] } stepBlock ||
            loop.IteratorName is not { } iterator)
        {
            return null;
        }

        int armIndex = step.Clauses.FindIndex(match: c => c.Pattern is ElsePattern { VariableName: not null });
        if (armIndex < 0)
        {
            return null;
        }

        WhenClause arm = step.Clauses[index: armIndex];
        string name = ((ElsePattern)arm.Pattern).VariableName!;
        if (name.StartsWith(value: "_lf_seen_", comparisonType: StringComparison.Ordinal) ||
            ElementOf(step: step) is not RecordTypeSymbol element || !IsChangeableValue(type: element) ||
            !ChangesThrough(body: arm.Body, name: name))
        {
            return null;
        }

        int n = _count++;
        Statement? body = null;
        if (MemoryWalkOf(emitter: emitter) is { } walk &&
            !AstWalker.BoundNames(node: arm.Body).Contains(item: name))
        {
            body = InPlace(body: arm.Body, name: name, element: element, emitter: emitter, walk: walk,
                iterator: iterator, n: n);
        }

        string seen = $"_lf_seen_{n}";
        if (body == null)
        {
            TypeRegistry.Lifecycle lifecycle = registry.GetLifecycle(type: element);
            if (lifecycle.IsBorrow || lifecycle.Destroy == null)
            {
                // Nothing the record holds is released by a member write, so the read is safe to change.
                return null;
            }

            body = Copied(body: arm.Body, name: name, seen: seen, element: element);
        }

        List<WhenClause> clauses = [.. step.Clauses];
        clauses[index: armIndex] = arm with
        {
            Pattern = new ElsePattern(VariableName: seen, Location: arm.Pattern.Location),
            Body = body
        };
        return loop with
        {
            Body = stepBlock with { Statements = [step with { Clauses = clauses }] }
        };
    }

    /// <summary>The element type a loop's step gives: the <c>T</c> of the <c>T?</c> it recovers.</summary>
    private static TypeSymbol? ElementOf(WhenStatement step)
    {
        return step.Expression.ResolvedType is RecordTypeSymbol { TypeArguments: [{ } element] } carrier &&
               carrier.CarrierKind != TypeModel.Enums.CarrierKind.None
            ? element
            : null;
    }

    /// <summary>Whether <paramref name="type"/> is a record or a tuple of member variables a program can change: not a
    /// handle, a pointer or a carrier, not an entity kind, and concrete.</summary>
    private bool IsChangeableValue(RecordTypeSymbol type)
    {
        return type.BackendType == null &&
               type.CarrierKind == TypeModel.Enums.CarrierKind.None &&
               TypeRegistry.GetRcWrapperBaseName(type: type) == null && !registry.IsEntityKind(type: type) &&
               !HasTypeParameter(type: type);
    }

    private static bool HasTypeParameter(TypeSymbol type)
    {
        return type is GenericParameterTypeSymbol or ProtocolSelfTypeSymbol or AssociatedProjectionTypeSymbol or
                   BuildtimeConstGenericTypeSymbol ||
               (type.TypeArguments?.Any(predicate: HasTypeParameter) ?? false);
    }

    /// <summary>The walk over the places of a memory a loop's emitter is (held through its shared handle), or null.</summary>
    private static EntityTypeSymbol? MemoryWalkOf(TypeSymbol emitter)
    {
        TypeSymbol walked = TypeRegistry.GetRcWrapperBaseName(type: emitter) == RuntimeContract.Roamed &&
                            emitter.TypeArguments is [{ } inner]
            ? inner
            : emitter;
        return walked is EntityTypeSymbol { GenericDefinition.Name: RuntimeContract.MemoryEmitter } walk
            ? walk
            : null;
    }

    /// <summary>Whether the body changes the record <paramref name="name"/> holds: writes one of its member variables,
    /// or calls a routine on it (or on a member of it) that is not marked as reading only.</summary>
    private static bool ChangesThrough(Statement body, string name)
    {
        bool changes = false;
        AstWalker.Walk(root: body,
            visit: node =>
            {
                changes |= node switch
                {
                    AssignmentStatement assign => RootedAt(expr: assign.Target, name: name, member: true),
                    BinaryExpression { Operator: BinaryOperator.Assign } bin =>
                        RootedAt(expr: bin.Left, name: name, member: true),
                    CompoundAssignmentExpression compound => RootedAt(expr: compound.Target, name: name, member: true),
                    CallExpression { Callee: MemberExpression { Object: var receiver } } call =>
                        call.ResolvedRoutine is not { IsReadOnly: true } &&
                        RootedAt(expr: receiver, name: name, member: false),
                    _ => false
                };
            });
        return changes;
    }

    /// <summary>Whether <paramref name="expr"/> is <paramref name="name"/> or a member variable or element reached
    /// from it. With <paramref name="member"/>, the name alone does not count.</summary>
    private static bool RootedAt(Expression expr, string name, bool member)
    {
        return expr switch
        {
            IdentifierExpression id => !member && id.Name == name,
            MemberExpression m => RootedAt(expr: m.Object, name: name, member: false),
            IndexExpression index => RootedAt(expr: index.Object, name: name, member: false),
            _ => false
        };
    }

    /// <summary>The body with the place the step read bound first and every use of <paramref name="name"/> made the
    /// record at that place.</summary>
    private Statement? InPlace(Statement body, string name, RecordTypeSymbol element, TypeSymbol emitter,
        EntityTypeSymbol walk, string iterator, int n)
    {
        // The routine of the walk itself, called through the handle the loop holds, as the loop's `emit` is.
        if (registry.LookupMemberRoutine(type: walk, memberRoutineName: RuntimeContract.EmittedPlace) is not
            { } placeRoutine)
        {
            return null;
        }

        SourceLocation loc = body.Location;
        TypeSymbol placeType = registry.GetOrCreateWrapperType(wrapperName: RuntimeContract.Hijacked,
            innerType: element,
            isReadOnly: false);
        string place = $"_lf_place_{n}";
        var placeCall = new CallExpression(
            Callee: new MemberExpression(Object: new IdentifierExpression(Name: iterator, Location: loc)
                {
                    ResolvedType = emitter
                },
                MemberName: RuntimeContract.EmittedPlace,
                Location: loc) { ResolvedType = emitter },
            Arguments: [],
            Location: loc)
        {
            ResolvedRoutine = placeRoutine,
            ResolvedType = placeType,
            LoweringKind = Builder.Verification.CallClassifier.ClassifyMemberRoutineCall(memberRoutine: placeRoutine),
            IsSynthesizedLowering = true
        };
        var placeDeclaration = new DeclarationStatement(
            Declaration: new VariableDeclaration(Name: place,
                Type: null,
                Initializer: placeCall,
                Visibility: VisibilityModifier.Secret,
                Location: loc),
            Location: loc);

        Statement reached = new ElementAtPlace(pass: this, name: name, place: place, placeType: placeType,
            element: element).VisitStatement(stmt: body);
        List<Statement> statements = reached is BlockStatement block
            ? [placeDeclaration, .. block.Statements]
            : [placeDeclaration, reached];
        return new BlockStatement(Statements: statements, Location: loc);
    }

    /// <summary>The body with the element copied into <paramref name="name"/> first.</summary>
    private static Statement Copied(Statement body, string name, string seen, RecordTypeSymbol element)
    {
        SourceLocation loc = body.Location;
        var copy = new DeclarationStatement(
            Declaration: new VariableDeclaration(Name: name,
                Type: null,
                Initializer: new IdentifierExpression(Name: seen, Location: loc) { ResolvedType = element },
                Visibility: VisibilityModifier.Secret,
                Location: loc) { IsLoopVariable = true },
            Location: loc);
        List<Statement> statements = body is BlockStatement block
            ? [copy, .. block.Statements]
            : [copy, body];
        return new BlockStatement(Statements: statements, Location: loc);
    }

    /// <summary>
    /// Makes each use of the loop variable the record at the place the step read. A lambda is not changed that way: it
    /// may be kept past the round, when the place may hold another value or be gone. A statement holding a lambda that
    /// uses the loop variable first copies the element into a local of its own (<c>var _lf_cap_N = t</c>, torn down
    /// with the block), and the lambda uses that copy: the element as it is when the statement runs.
    /// </summary>
    private sealed class ElementAtPlace(
        RecordLoopElementPass pass,
        string name,
        string place,
        TypeSymbol placeType,
        RecordTypeSymbol element) : AstRewriter
    {
        public override Expression VisitExpression(Expression expr)
        {
            return expr is IdentifierExpression id && id.Name == name
                ? Element(loc: id.Location)
                : base.VisitExpression(expr: expr);
        }

        protected override Statement VisitBlock(BlockStatement s)
        {
            bool changed = false;
            var statements = new List<Statement>(capacity: s.Statements.Count);
            foreach (Statement stmt in s.Statements)
            {
                // Inner blocks are done first, so a lambda still using the name sits in this statement itself.
                Statement visited = VisitStatement(stmt: stmt);
                if (UsedByLambda(stmt: visited, name: name))
                {
                    string copy = $"_lf_cap_{pass._count++}";
                    statements.Add(item: new DeclarationStatement(
                        Declaration: new VariableDeclaration(Name: copy,
                            Type: null,
                            Initializer: Element(loc: stmt.Location),
                            Visibility: VisibilityModifier.Secret,
                            Location: stmt.Location),
                        Location: stmt.Location));
                    visited = new LambdaUses(from: name, to: copy, type: element).VisitStatement(stmt: visited);
                }

                changed |= !ReferenceEquals(objA: visited, objB: stmt);
                statements.Add(item: visited);
            }

            return changed
                ? s with { Statements = statements }
                : s;
        }

        private WrapperProjectionExpression Element(SourceLocation loc)
        {
            return new WrapperProjectionExpression(
                Wrapper: new IdentifierExpression(Name: place, Location: loc) { ResolvedType = placeType },
                Kind: WrapperProjectionKind.RecordAddress,
                Controller: null,
                FieldIndex: 0,
                Location: loc) { ResolvedType = element, ReadsLoopElement = true };
        }
    }

    /// <summary>Whether a lambda in <paramref name="stmt"/> uses <paramref name="name"/>.</summary>
    private static bool UsedByLambda(Statement stmt, string name)
    {
        bool used = false;
        AstWalker.WalkExpressions(root: stmt,
            visit: e =>
            {
                if (e is LambdaExpression lambda && lambda.Parameters.All(predicate: p => p.Name != name))
                {
                    AstWalker.WalkExpressions(root: lambda.Body,
                        visit: inner => used |= inner is IdentifierExpression id && id.Name == name);
                }
            });
        return used;
    }

    /// <summary>Gives the lambdas of a statement another name for what they use as <paramref name="from"/>.</summary>
    private sealed class LambdaUses(string from, string to, TypeSymbol type) : AstRewriter
    {
        private bool _inLambda;

        public override Expression VisitExpression(Expression expr)
        {
            switch (expr)
            {
                case IdentifierExpression id when _inLambda && id.Name == from:
                    return id with { Name = to, ResolvedType = type };
                case LambdaExpression lambda when lambda.Parameters.All(predicate: p => p.Name != from):
                {
                    bool outer = _inLambda;
                    _inLambda = true;
                    Expression body = VisitExpression(expr: lambda.Body);
                    _inLambda = outer;
                    return lambda with
                    {
                        Body = body,
                        Captures = lambda.Captures?.Select(selector: c => c == from
                                             ? to
                                             : c)
                                         .ToList()
                    };
                }
                default:
                    return base.VisitExpression(expr: expr);
            }
        }
    }
}
