using Builder.Frontends;
using Revare;

namespace Mielia;

/// <summary>
/// The Mielia front end: registers Mielia's rules with the builder core, together with Revare's, whose
/// standard library a Mielia build analyzes.
/// </summary>
public static class MieliaFrontend
{
    /// <summary>Registers Mielia's and Revare's rules (idempotent).</summary>
    public static void Register()
    {
        RevareFrontend.Register();
        Languages.Register(rules: MieliaRules.Instance);
    }
}
