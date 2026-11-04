package com.mielia.rider

import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.colors.CodeInsightColors
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.PlainSyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.options.colors.AttributesDescriptor
import com.intellij.openapi.options.colors.ColorDescriptor
import com.intellij.openapi.options.colors.ColorSettingsPage
import com.intellij.platform.lsp.api.customization.LspSemanticTokensSupport
import javax.swing.Icon

/**
 * Mielia's own colors. Each starts as the C# color of its counterpart (record as struct, entity as class,
 * routine as method, module as namespace, global and preset as constant), so Mielia reads like C# in any scheme until
 * Settings | Editor | Color Scheme | Mielia says otherwise. A protocol and an annotation are the
 * exceptions: a protocol starts light blue and italic, an annotation yellow (colorSchemes/).
 */
object MieliaColors {
    val RECORD = key("MIELIA_RECORD", TextAttributesKey.find("ReSharper.STRUCT_IDENTIFIER"))
    val ENTITY = key("MIELIA_ENTITY", DefaultLanguageHighlighterColors.CLASS_NAME)
    val PROTOCOL = key("MIELIA_PROTOCOL", DefaultLanguageHighlighterColors.INTERFACE_NAME)
    val GENERIC_PARAMETER = key("MIELIA_GENERIC_PARAMETER", TextAttributesKey.find("ReSharper.TYPE_PARAMETER_IDENTIFIER"))
    val ROUTINE = key("MIELIA_ROUTINE", DefaultLanguageHighlighterColors.INSTANCE_METHOD)
    val MODULE = key("MIELIA_MODULE", TextAttributesKey.find("ReSharper.NAMESPACE_IDENTIFIER"))
    val PRESET = key("MIELIA_PRESET", DefaultLanguageHighlighterColors.CONSTANT)
    val OPERATOR = key("MIELIA_OPERATOR", DefaultLanguageHighlighterColors.OPERATION_SIGN)
    val DOC_TAG = key("MIELIA_DOC_TAG", DefaultLanguageHighlighterColors.DOC_COMMENT_TAG)
    val DOC_VALUE = key("MIELIA_DOC_VALUE", DefaultLanguageHighlighterColors.DOC_COMMENT_TAG_VALUE)
    val ANNOTATION = key("MIELIA_ANNOTATION", DefaultLanguageHighlighterColors.METADATA)

    /** A keyword the next version drops or renames: struck through over its keyword color. */
    val RETIRED = key("MIELIA_RETIRED_KEYWORD", CodeInsightColors.DEPRECATED_ATTRIBUTES)

    private fun key(name: String, csharp: TextAttributesKey) = TextAttributesKey.createTextAttributesKey(name, csharp)
}

/** The colors of the semantic token types the Mielia language server sends that the platform doesn't know. */
internal object MieliaSemanticTokens : LspSemanticTokensSupport() {
    private val keys = mapOf(
        "recordType" to MieliaColors.RECORD,
        "entityType" to MieliaColors.ENTITY,
        "interface" to MieliaColors.PROTOCOL,
        "typeParameter" to MieliaColors.GENERIC_PARAMETER,
        "function" to MieliaColors.ROUTINE,
        "namespace" to MieliaColors.MODULE,
        "constant" to MieliaColors.PRESET,
        "operator" to MieliaColors.OPERATOR,
        "docTag" to MieliaColors.DOC_TAG,
        "docValue" to MieliaColors.DOC_VALUE,
        "decorator" to MieliaColors.ANNOTATION,
    )

    override val tokenTypes: List<String> = (super.tokenTypes + keys.keys).distinct()

    override fun getTextAttributesKey(tokenType: String, modifiers: List<String>): TextAttributesKey? =
        if ("retired" in modifiers) MieliaColors.RETIRED
        else keys[tokenType] ?: super.getTextAttributesKey(tokenType, modifiers)
}

/** Settings | Editor | Color Scheme | Mielia. */
class MieliaColorSettingsPage : ColorSettingsPage {
    private val descriptors = arrayOf(
        AttributesDescriptor(MieliaBundle.message("color.types.record"), MieliaColors.RECORD),
        AttributesDescriptor(MieliaBundle.message("color.types.entity"), MieliaColors.ENTITY),
        AttributesDescriptor(MieliaBundle.message("color.types.protocol"), MieliaColors.PROTOCOL),
        AttributesDescriptor(MieliaBundle.message("color.types.generic"), MieliaColors.GENERIC_PARAMETER),
        AttributesDescriptor(MieliaBundle.message("color.routine"), MieliaColors.ROUTINE),
        AttributesDescriptor(MieliaBundle.message("color.module"), MieliaColors.MODULE),
        AttributesDescriptor(MieliaBundle.message("color.preset"), MieliaColors.PRESET),
        AttributesDescriptor(MieliaBundle.message("color.operator"), MieliaColors.OPERATOR),
        AttributesDescriptor(MieliaBundle.message("color.doc.tag"), MieliaColors.DOC_TAG),
        AttributesDescriptor(MieliaBundle.message("color.doc.value"), MieliaColors.DOC_VALUE),
        AttributesDescriptor(MieliaBundle.message("color.annotation"), MieliaColors.ANNOTATION),
    )

    private val tags = mapOf(
        "record" to MieliaColors.RECORD,
        "entity" to MieliaColors.ENTITY,
        "protocol" to MieliaColors.PROTOCOL,
        "generic" to MieliaColors.GENERIC_PARAMETER,
        "routine" to MieliaColors.ROUTINE,
        "module" to MieliaColors.MODULE,
        "preset" to MieliaColors.PRESET,
        "op" to MieliaColors.OPERATOR,
        "annotation" to MieliaColors.ANNOTATION,
    )

    override fun getDisplayName(): String = "Mielia"

    override fun getIcon(): Icon = MieliaIconProvider.ICON

    override fun getHighlighter(): SyntaxHighlighter = PlainSyntaxHighlighter()

    override fun getAttributeDescriptors(): Array<AttributesDescriptor> = descriptors

    override fun getColorDescriptors(): Array<ColorDescriptor> = ColorDescriptor.EMPTY_ARRAY

    override fun getAdditionalHighlightingTagToDescriptorMap(): Map<String, TextAttributesKey> = tags

    override fun getDemoText(): String = """
        import <module>IO</module>/<module>Console</module>

        global <preset>visits</preset>: <record>Int</record> = 0

        choice <record>Shape</record>
            CIRCLE
            SQUARE

        record <record>Point</record>
            x: <record>Int</record>
            y: <record>Int</record>

        entity <entity>Account</entity> obeys <protocol>Displayable</protocol>
            owner: <record>Text</record>
            history: <entity>List</entity>[<record>Point</record>]

        routine <entity>Account</entity>.<routine>deposit</routine>(amount: <record>Int</record>)
            me.balance <op>+=</op> amount
            return

        <annotation>@positional</annotation>
        routine <routine>largest</routine>[<generic>T</generic>](items: <entity>List</entity>[<generic>T</generic>]) -> <generic>T</generic>
            return items.<routine>first</routine>()

        routine <routine>start</routine>()
            <preset>visits</preset> <op>+=</op> 1
            <routine>show</routine>(<preset>visits</preset>)
            return
    """.trimIndent()
}
