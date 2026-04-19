using System.Text.RegularExpressions;

namespace FieldCure.DocumentParsers;

/// <summary>
/// Normalizes Hancom equation script (HML/HWPX format) to LaTeX notation.
/// Architecture based on hml-equation-parser (Apache 2.0, OpenBapul/hml-equation-parser)
/// with HWPX-specific extensions:
/// <list type="bullet">
///   <item>Uppercase <c>OVER</c> support (HWPX uses <c>OVER</c>, not <c>over</c>)</item>
///   <item>Double-brace accent notation (e.g., <c>hat {{x}}</c>)</item>
///   <item>Tilde <c>~</c> spacing conversion</item>
///   <item><c>UNDEROVER ∫</c> integral pattern</item>
///   <item><c>REL rarrow {label}</c> labeled-arrow pattern</item>
/// </list>
/// </summary>
internal static partial class HancomMathNormalizer
{
    #region Conversion Maps

    /// <summary>
    /// Direct token → LaTeX substitution map.
    /// Source: hml-equation-parser/convertMap.json (Apache 2.0) + HWPX extensions.
    /// Tokens mapped to empty string are silently removed.
    /// </summary>
    private static readonly Dictionary<string, string> ConvertMap =
        new(StringComparer.Ordinal)
        {
            // ── Set / Logic ────────────────────────────────────────────────
            ["TIMES"] = @"\times",      ["times"] = @"\times",
            ["SMALLSUM"] = @"\sum",     ["sum"] = @"\sum",
            ["SMALLPROD"] = @"\prod",   ["prod"] = @"\prod",
            ["SMALLINTER"] = @"\cap",
            ["CUP"] = @"\cup",          ["inter"] = @"\bigcap",
            ["union"] = @"\bigcup",
            // Spec §1.2: UNION/INTER are the big operator forms (take sub/sup).
            // SMALLUNION/SMALLINTER are the inline (∪/∩) forms.
            ["UNION"] = @"\bigcup",     ["INTER"] = @"\bigcap",
            ["SMALLUNION"] = @"\cup",   ["CAP"] = @"\cap",
            ["PROD"] = @"\prod",        ["COPROD"] = @"\coprod",
            ["VEE"] = @"\vee",          ["WEDGE"] = @"\wedge",
            ["OSLASH"] = @"\oslash",
            ["OPLUS"] = @"\oplus",      ["OMINUS"] = @"\ominus",
            ["OTIMES"] = @"\otimes",    ["ODIV"] = @"\oslash",
            ["ODOT"] = @"\odot",
            ["LOR"] = @"\lor",          ["LAND"] = @"\land",
            ["SUBSET"] = @"\subset",    ["SUPERSET"] = @"\supset",
            ["SUPSET"] = @"\supset",
            ["SUBSETEQ"] = @"\subseteq", ["SUPSETEQ"] = @"\supseteq",
            ["IN"] = @"\in",            ["OWNS"] = @"\owns",
            ["NOTIN"] = @"\notin",      ["notin"] = @"\notin",
            ["SQCAP"] = @"\sqcap",      ["SQCUP"] = @"\sqcup",
            ["SQSUBSET"] = @"\sqsubset", ["SQSUBSETEQ"] = @"\sqsubseteq",
            ["SQSUPSET"] = @"\sqsupset", ["SQSUPSETEQ"] = @"\sqsupseteq",
            ["LLL"] = @"\lll",
            ["BIGSQCUP"] = @"\bigsqcup",
            ["BIGOPLUS"] = @"\bigoplus", ["BIGOTIMES"] = @"\bigotimes",
            ["BIGODOT"] = @"\bigodot",  ["BIGUPLUS"] = @"\biguplus",
            ["SMCOPROD"] = @"\coprod",  ["coprod"] = @"\coprod",
            ["BIGOMINUS"] = @"{{\Large\ominus}}",
            ["BIGODIV"] = @"{{\Large\oslash}}",

            // ── Relations ──────────────────────────────────────────────────
            ["LEQ"] = @"\leq",       ["GEQ"] = @"\geq",
            ["leq"] = @"\leq",       ["geq"] = @"\geq",
            ["<<"] = @"\ll",         [">>"] = @"\gg",
            ["<<<"] = @"\lll",       [">>>"] = @"\ggg",
            ["PREC"] = @"\prec",     ["SUCC"] = @"\succ",
            ["SIM"] = @"\sim",
            ["APPROX"] = @"\approx", ["approx"] = @"\approx",
            ["SIMEQ"] = @"\simeq",   ["CONG"] = @"\cong",
            ["=="] = @"\equiv",      ["equiv"] = @"\equiv",
            ["IDENTICAL"] = @"\equiv", ["EQUIV"] = @"\equiv",
            ["!="] = @"\neq",        ["neq"] = @"\neq",
            ["DOTEQ"] = @"\doteq",   ["ASYMP"] = @"\asymp",
            ["ISO"] = @"\Bumpeq",    ["DSUM"] = @"\dotplus",
            ["XOR"] = @"\veebar",
            ["⊐"] = @"\sqsupset",   ["⊒"] = @"\sqsupseteq",

            // ── Arithmetic ────────────────────────────────────────────────
            ["±"] = @"\pm",          ["pm"] = @"\pm",
            ["PLUSMINUS"] = @"\pm",  ["MINUSPLUS"] = @"\mp",
            ["-+"] = @"\mp",         ["mp"] = @"\mp",
            ["÷"] = @"\div",         ["div"] = @"\div",
            ["DIV"] = @"\div",       ["DIVIDE"] = @"\div",
            ["UPLUS"] = @"\uplus",
            ["CIRC"] = @"\circ",     ["BULLET"] = @"\bullet",
            ["DEG"] = @" ^\circ",
            ["AST"] = @"\ast",       ["STAR"] = @"\bigstar",
            ["BIGCIRC"] = @"\bigcirc",
            ["cdot"] = @"\cdot",

            // ── Delimiters ────────────────────────────────────────────────
            ["LEFT"] = @"\left",     ["RIGHT"] = @"\right",
            ["left"] = @"\left",     ["right"] = @"\right",
            ["⌈"] = @"\lceil",      ["⌉"] = @"\rceil",
            ["⌊"] = @"\lfloor",     ["⌋"] = @"\rfloor",
            ["∥"] = @"\|",          ["PVER"] = @"\|",

            // ── Calculus / Analysis ───────────────────────────────────────
            ["sqrt"] = @"\sqrt",
            ["int"] = @"\int",       ["dint"] = @"\iint",
            ["tint"] = @"\iiint",    ["oint"] = @"\oint",
            ["lim"] = @"\lim",       ["Lim"] = @"\lim",
            ["Partial"] = @"\partial",  ["PARTIAL"] = @"\partial",
            ["partial"] = @"\partial",
            ["INF"] = @"\infty",     ["inf"] = @"\infty",
            ["NABLA"] = @"\nabla",   ["PROPTO"] = @"\propto",
            // Unicode integral/sum/product symbols embedded directly in HWPX scripts
            ["∫"] = @"\int",         ["∬"] = @"\iint",
            ["∭"] = @"\iiint",      ["∮"] = @"\oint",
            ["∑"] = @"\sum",         ["∏"] = @"\prod",

            // ── Misc math ─────────────────────────────────────────────────
            ["EMPTYSET"] = @"\emptyset",
            ["THEREFORE"] = @"\therefore", ["BECAUSE"] = @"\because",
            ["EXIST"] = @"\exists",  ["FORALL"] = @"\forall",
            ["prime"] = @"'",        ["DIAMOND"] = @"\diamond",
            ["LNOT"] = @"\lnot",     ["NOT"] = @"\neg",
            ["IMAG"] = @"\Im",       ["image"] = @"\Im",
            ["REIMAGE"] = @"\Re",    ["reimage"] = @"\Re",
            ["LAPLACE"] = @"\mathcal{L}",
            ["TRIANGLE"] = @"\triangle",
            ["TRIANGLED"] = @"\triangledown",
            ["RTANGLE"] = @"\sphericalangle",
            ["ANGLE"] = @"\angle",   ["MSANGLE"] = @"\measuredangle",
            ["SANGLE"] = @"\sphericalangle",
            ["VDASH"] = @"\vdash",   ["DASHV"] = @"\dashv",
            ["HLEFT"] = @"\dashv",   // spec §1.2.4.6 pair: VDASH=⊢ / HLEFT=⊣
            ["BOT"] = @"\bot",       ["TOP"] = @"\top",
            ["MODELS"] = @"\models",
            ["CDOTS"] = @"\cdots",   ["cdots"] = @"\cdots",
            ["LDOTS"] = @"\ldots",   ["ldots"] = @"\ldots",
            ["VDOTS"] = @"\vdots",   ["vdots"] = @"\vdots",
            ["DDOTS"] = @"\ddots",   ["ddots"] = @"\ddots",
            ["DAGGER"] = @"\dagger", ["DDAGGER"] = @"\ddagger",
            ["MAPSTO"] = @"\mapsto", ["under"] = @"\underline",
            ["LSLANT"] = @"\diagup", ["RSLANT"] = @"\diagdown",
            ["CENTIGRADE"] = @"^{\circ}C",
            ["FAHRENHEIT"] = @"^{\circ}F",

            // ── Arrows ────────────────────────────────────────────────────
            ["larrow"] = @"\leftarrow",
            ["rarrow"] = @"\rightarrow",
            ["lrarrow"] = @"\leftrightarrow",
            ["Larrow"] = @"\Leftarrow",
            ["Rarrow"] = @"\Rightarrow",
            ["LRarrow"] = @"\Leftrightarrow",
            ["uarrow"] = @"\uparrow",
            ["darrow"] = @"\downarrow",
            ["uparrow"] = @"\uparrow",      ["downarrow"] = @"\downarrow",
            ["updownarrow"] = @"\updownarrow",
            ["LARROW"] = @"\Leftarrow",
            ["RARROW"] = @"\Rightarrow",
            ["UPARROW"] = @"\Uparrow",
            ["DOWNARROW"] = @"\Downarrow",
            ["udarrow"] = @"\updownarrow",
            ["<->"] = @"\leftrightarrow",
            ["->"] = @"\rightarrow",
            ["UDARROW"] = @"\Updownarrow",
            ["LRARROW"] = @"\Leftrightarrow",
            ["NWARROW"] = @"\nwarrow",   ["SEARROW"] = @"\searrow",
            ["NEARROW"] = @"\nearrow",   ["SWARROW"] = @"\swarrow",
            ["nwarrow"] = @"\nwarrow",   ["searrow"] = @"\searrow",
            ["nearrow"] = @"\nearrow",   ["swarrow"] = @"\swarrow",
            ["HOOKLEFT"] = @"\hookleftarrow",
            ["HOOKRIGHT"] = @"\hookrightarrow",
            ["hookleft"] = @"\hookleftarrow",
            ["hookright"] = @"\hookrightarrow",
            ["mapsto"] = @"\mapsto",
            ["vert"] = @"\vert",         ["VERT"] = @"\Vert",

            // ── Greek (lowercase keyword form) ────────────────────────────
            // Note: HWPX typically embeds Unicode directly (τ, λ, γ, …).
            // These entries handle keyword-form input.
            ["alpha"] = @"\alpha",   ["beta"] = @"\beta",
            ["gamma"] = @"\gamma",   ["delta"] = @"\delta",
            ["epsilon"] = @"\epsilon", ["zeta"] = @"\zeta",
            ["eta"] = @"\eta",       ["theta"] = @"\theta",
            ["iota"] = @"\iota",     ["kappa"] = @"\kappa",
            ["lambda"] = @"\lambda", ["mu"] = @"\mu",
            ["nu"] = @"\nu",         ["xi"] = @"\xi",
            ["omicron"] = @"\omicron", ["pi"] = @"\pi",
            ["rho"] = @"\rho",       ["sigma"] = @"\sigma",
            ["tau"] = @"\tau",       ["upsilon"] = @"\upsilon",
            ["phi"] = @"\phi",       ["chi"] = @"\chi",
            ["psi"] = @"\psi",       ["omega"] = @"\omega",

            // ── Greek (uppercase keyword form) ────────────────────────────
            ["ALPHA"] = "A",         ["BETA"] = "B",
            ["GAMMA"] = @"\Gamma",   ["DELTA"] = @"\Delta",
            ["EPSILON"] = "E",       ["ZETA"] = "Z",
            ["ETA"] = "H",           ["THETA"] = @"\Theta",
            ["IOTA"] = "I",          ["KAPPA"] = "K",
            ["LAMBDA"] = @"\Lambda", ["MU"] = "M",
            ["NU"] = "N",            ["XI"] = @"\Xi",
            ["OMICRON"] = "O",       ["PI"] = @"\Pi",
            ["RHO"] = "P",           ["SIGMA"] = @"\Sigma",
            ["TAU"] = "T",           ["UPSILON"] = @"\Upsilon",
            ["PHI"] = @"\Phi",       ["CHI"] = "X",
            ["PSI"] = @"\Psi",       ["OMEGA"] = @"\Omega",

            // ── Greek (Pascal-case) ───────────────────────────────────────
            // Spec §1.2 mandates Pascal-case input for Greek uppercase.
            // Ambiguous letters (A, B, E, Z, H, I, K, M, N, O, P, T, X) map
            // to Latin since LaTeX has no dedicated \Alpha etc.
            ["Alpha"] = "A",         ["Beta"] = "B",
            ["Gamma"] = @"\Gamma",   ["Delta"] = @"\Delta",
            ["Epsilon"] = "E",       ["Zeta"] = "Z",
            ["Eta"] = "H",           ["Theta"] = @"\Theta",
            ["Iota"] = "I",          ["Kappa"] = "K",
            ["Lambda"] = @"\Lambda", ["Mu"] = "M",
            ["Nu"] = "N",            ["Xi"] = @"\Xi",
            ["Omicron"] = "O",       ["Pi"] = @"\Pi",
            ["Rho"] = "P",           ["Sigma"] = @"\Sigma",
            ["Tau"] = "T",           ["Upsilon"] = @"\Upsilon",
            ["Phi"] = @"\Phi",       ["Chi"] = "X",
            ["Psi"] = @"\Psi",       ["Omega"] = @"\Omega",

            // ── Math letters (spec §1.2.4, 수학 문자) ───────────────────
            ["ALEPH"] = @"\aleph",       ["HBAR"] = @"\hbar",
            ["IMATH"] = @"\imath",       ["JMATH"] = @"\jmath",
            ["OHM"] = @"\Omega",         ["ELL"] = @"\ell",
            ["Liter"] = @"\ell",         ["WP"] = @"\wp",
            ["ANGSTROM"] = @"\AA",
            ["vartheta"] = @"\vartheta", ["varpi"] = @"\varpi",
            ["varsigma"] = @"\varsigma", ["varupsilon"] = @"\varUpsilon",
            ["varphi"] = @"\varphi",     ["varepsilon"] = @"\varepsilon",

            // ── Trig / Functions (spec §1.1.3 roman-type) ──────────────────
            ["log"] = @"\log",       ["ln"] = @"\ln",
            ["lg"] = @"\lg",         ["lcm"] = @"\lcm",
            ["sin"] = @"\sin",       ["cos"] = @"\cos",
            ["tan"] = @"\tan",       ["cot"] = @"\cot",
            ["sec"] = @"\sec",       ["csc"] = @"\csc",
            ["cosec"] = @"\csc",
            ["sinh"] = @"\sinh",     ["cosh"] = @"\cosh",
            ["tanh"] = @"\tanh",     ["coth"] = @"\coth",
            ["arcsin"] = @"\arcsin", ["arccos"] = @"\arccos",
            ["arctan"] = @"\arctan",
            ["asin"] = @"\arcsin",   ["acos"] = @"\arccos",
            ["atan"] = @"\arctan",
            ["exp"] = @"\exp",       ["Exp"] = @"\exp",
            ["max"] = @"\max",       ["min"] = @"\min",
            ["det"] = @"\det",       ["gcd"] = @"\gcd",
            ["mod"] = @"\bmod",
            ["arg"] = @"\arg",       ["deg"] = @"\deg",
            ["dim"] = @"\dim",       ["ker"] = @"\ker",
            ["hom"] = @"\hom",       ["Pr"] = @"\Pr",

            // ── Font switches (spec §1.1.1.1) ──────────────────────────────
            // LaTeX's \mathrm/\mathit/\mathbf take the following braced group
            // as argument. When the spec input is `rm {body}` the result is
            // correct; `rm body` (no braces) makes only the first char roman.
            // That's the same degraded behavior the Hancom editor exhibits.
            ["rm"] = @"\mathrm",
            ["it"] = @"\mathit",
            ["bold"] = @"\mathbf",

            // ── HWPX-specific ─────────────────────────────────────────────
            // OVER: HWPX uses uppercase; normalize to lowercase for ReplaceFrac
            ["OVER"] = "over",
            // from / to — Hancom structural keywords for limits on big operators
            // (int, sum, prod, lim, …). Render as _/^. Spec §1.2 lim example:
            //   "y = lim _{x -> 0} {1 over x}"  — uses native _/^ form,
            // but "int from A to B" is equally valid and maps identically.
            ["from"] = "_",            ["to"] = "^",
            // UNDEROVER: structural keyword before nary symbol (∫, ∑ …).
            // Removing it leaves the symbol to naturally take _ and ^ limits.
            ["UNDEROVER"] = "",
            // REL: handled by regex pre-pass; remove any remnants
            ["REL"] = "",
            ["BUILDREL"] = "",
        };

    /// <summary>
    /// Tokens replaced with internal HULK markers for deferred structural processing.
    /// (accent/matrix operators that need bracket-matching transforms)
    /// </summary>
    private static readonly Dictionary<string, string> MiddleConvertMap =
        new(StringComparer.Ordinal)
        {
            ["matrix"] = "HULKMATRIX",   ["pmatrix"] = "HULKPMATRIX",
            ["bmatrix"] = "HULKBMATRIX", ["dmatrix"] = "HULKDMATRIX",
            ["eqalign"] = "HULKEQALIGN", ["cases"] = "HULKCASE",
            ["vec"] = "HULKVEC",         ["dyad"] = "HULKDYAD",
            ["acute"] = "HULKACUTE",     ["grave"] = "HULKGRAVE",
            ["dot"] = "HULKDOT",         ["ddot"] = "HULKDDOT",
            ["bar"] = "HULKBAR",         ["hat"] = "HULKHAT",
            ["check"] = "HULKCHECK",     ["arch"] = "HULKARCH",
            ["tilde"] = "HULKTILDE",     ["BOX"] = "HULKBOX",
            ["box"] = "HULKBOX",
            // pile/lpile/rpile: spec §1.2 vertical stack (centre/left/right).
            // All three collapse to \begin{matrix} — LaTeX's default matrix is
            // centred so lpile/rpile alignment distinction is lost but content
            // is preserved (acceptable for LLM extraction target).
            ["pile"] = "HULKPILE",       ["lpile"] = "HULKPILE",
            ["rpile"] = "HULKPILE",
            ["OVERBRACE"] = "HULKOVERBRACE",
            ["UNDERBRACE"] = "HULKUNDERBRACE",
        };

    /// <summary>HULK accent marker → LaTeX accent command.</summary>
    private static readonly Dictionary<string, string> BarConvertMap =
        new(StringComparer.Ordinal)
        {
            ["HULKVEC"] = @"\overrightarrow",
            ["HULKDYAD"] = @"\overleftrightarrow",
            ["HULKACUTE"] = @"\acute",
            ["HULKGRAVE"] = @"\grave",
            ["HULKDOT"] = @"\dot",
            ["HULKDDOT"] = @"\ddot",
            ["HULKBAR"] = @"\overline",
            ["HULKHAT"] = @"\widehat",
            ["HULKCHECK"] = @"\check",
            ["HULKARCH"] = @"\overset{\frown}",
            ["HULKTILDE"] = @"\widetilde",
            ["HULKBOX"] = @"\boxed",
        };

    /// <summary>HULK matrix marker → LaTeX matrix environment.</summary>
    private static readonly Dictionary<string, (string Begin, string End, bool RemoveOuter)>
        MatrixConvertMap = new(StringComparer.Ordinal)
        {
            ["HULKMATRIX"]  = (@"\begin{matrix}",  @"\end{matrix}",  true),
            ["HULKPMATRIX"] = (@"\begin{pmatrix}", @"\end{pmatrix}", true),
            ["HULKBMATRIX"] = (@"\begin{bmatrix}", @"\end{bmatrix}", true),
            ["HULKDMATRIX"] = (@"\begin{vmatrix}", @"\end{vmatrix}", true),
            ["HULKCASE"]    = (@"\begin{cases}",   @"\end{cases}",   true),
            ["HULKPILE"]    = (@"\begin{matrix}",  @"\end{matrix}",  true),
            ["HULKEQALIGN"] = (@"\eqalign{",       "}",              false),
        };

    /// <summary>HULK brace marker → LaTeX brace command.</summary>
    private static readonly Dictionary<string, string> BraceConvertMap =
        new(StringComparer.Ordinal)
        {
            ["HULKOVERBRACE"]  = @"\overbrace",
            ["HULKUNDERBRACE"] = @"\underbrace",
        };

    #endregion

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Converts Hancom equation script text to LaTeX notation.
    /// </summary>
    public static string ToLaTeX(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
            return "";

        // Remove Hancom accessibility prefix
        var result = script.Replace("수식입니다.", "").Trim();

        // Pre-pass: normalize nth-root shorthand `^N sqrt` to `root {N} of`
        // so ReplaceRootOf handles all forms uniformly. Covers:
        //   ^3sqrt{x}      ^ 3 sqrt{x}     ^{3}sqrt{x}     ^{3} sqrt {x}
        // Padding with spaces on both sides protects against adjacent tokens
        // gluing (e.g. `3 \`^3sqrt` where ` followed by ^).
        result = NthRootShorthandRegex().Replace(result, " root {$1} of ");

        // Pre-pass: BUILDREL drops the lower-label arm (spec §1.2). Strip any
        // trailing {lower} group so the upper label is all RelArrowRegex sees.
        //   BUILDREL <arrow> {upper} {lower} → BUILDREL <arrow> {upper}
        result = BuildrelDropLowerRegex().Replace(result, "$1");

        // Pre-pass: REL arrow pattern before tokenization
        //   REL rarrow  {label} {} → \xrightarrow{label}
        //   REL larrow  {label} {} → \xleftarrow{label}
        //   REL lrarrow {label} {} → \xleftrightarrow{label}   (bidirectional)
        // Check for bidi forms FIRST (lrarrow/udarrow/LRARROW/UDARROW/LRarrow/
        // UDArrow) because those contain "rarrow" as a substring and would
        // otherwise fall through to \xrightarrow.
        result = RelArrowRegex().Replace(result, match =>
        {
            var arrowKw = match.Groups[1].Value.ToLowerInvariant();
            var label   = match.Groups[2].Value.Trim();
            var cmd = arrowKw.Contains("lrarrow") || arrowKw.Contains("udarrow")
                        ? @"\xleftrightarrow"
                    : arrowKw.Contains("larrow")
                        ? @"\xleftarrow"
                        : @"\xrightarrow";
            return string.IsNullOrEmpty(label) ? $"{cmd}{{}}" : $"{cmd}{{{label}}}";
        });

        // Tokenize: backticks → space; pad braces, pipes, & , ( ) with spaces
        // | must be padded so that "RIGHT |LEFT" doesn't merge into "|LEFT" token
        // , must be padded so keywords like "alpha, beta" tokenize as
        //   [alpha] [,] [beta] instead of [alpha,] [beta] (unmatched in map)
        // ( ) padding unglues function-call forms like `sinh(x)` → [sinh] [(] [x] [)]
        // so `sinh` is seen as a standalone token the map can match. Also fixes
        // `LEFT(...` glued forms.
        result = result.Replace("`", " ")
                       .Replace("{", " { ")
                       .Replace("}", " } ")
                       .Replace("|", " | ")
                       .Replace("&", " & ")
                       .Replace(",", " , ")
                       .Replace("(", " ( ")
                       .Replace(")", " ) ");

        // Split on any whitespace, not just ' '. HWPX equation scripts can have
        // literal \n / \r / \t between tokens (especially when authored with
        // Enter for line breaks inside the equation editor). Splitting only on
        // ' ' previously glued newline-adjacent tokens like `\nDIV` together,
        // causing map lookups to miss dozens of otherwise-mapped keywords.
        var tokens = result.Split(
            new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Token mapping (ConvertMap takes priority over MiddleConvertMap)
        for (var i = 0; i < tokens.Count; i++)
        {
            if (ConvertMap.TryGetValue(tokens[i], out var conv))
                tokens[i] = conv;
            else if (MiddleConvertMap.TryGetValue(tokens[i], out var mid))
                tokens[i] = mid;
        }

        // Remove tokens mapped to "" (UNDEROVER, REL, etc.)
        tokens = tokens.Where(t => t.Length > 0).ToList();

        // Fix: \left { → \left \{   and   \right } → \right \}
        for (var i = 1; i < tokens.Count; i++)
        {
            if (tokens[i] == "{" && tokens[i - 1] == @"\left")  tokens[i] = @"\{";
            if (tokens[i] == "}" && tokens[i - 1] == @"\right") tokens[i] = @"\}";
        }

        result = string.Join(" ", tokens);

        // Structural transforms (order matters)
        result = ReplaceFrac(result);
        result = ReplaceRootOf(result);
        result = ReplaceChoose(result);       // A CHOOSE B   → \binom{A}{B}
        result = ReplaceBinomPrefix(result);  // binom A B    → \binom{A}{B}
        result = ReplaceAtop(result);         // A atop B     → {A \atop B}
        result = ReplaceAllMatrix(result);
        result = ReplaceAllBar(result);
        result = ReplaceAllBrace(result);

        // ~ is Hancom math spacing notation → convert to space for clean LaTeX
        result = result.Replace("~", " ");

        // Normalize whitespace
        result = MultiSpaceRegex().Replace(result, " ").Trim();

        return result;
    }

    // ── Bracket utilities ─────────────────────────────────────────────────

    /// <summary>
    /// Finds the balanced <c>{ }</c> group starting at or after <paramref name="fromIndex"/>.
    /// Returns <c>(startInclusive, endExclusive)</c>.
    /// </summary>
    private static (int Start, int End) FindBracketsForward(string s, int fromIndex)
    {
        var start = s.IndexOf('{', fromIndex);
        if (start < 0) throw new InvalidOperationException("No opening bracket");
        var depth = 1;
        for (var i = start + 1; i < s.Length; i++)
        {
            if      (s[i] == '{') depth++;
            else if (s[i] == '}') { depth--; if (depth == 0) return (start, i + 1); }
        }
        throw new InvalidOperationException("Unmatched bracket");
    }

    /// <summary>
    /// Finds the balanced <c>{ }</c> group whose closing <c>}</c> appears immediately
    /// before <paramref name="beforeIndex"/> (skipping whitespace).
    /// Returns <c>(startInclusive, endExclusive)</c>.
    /// </summary>
    private static (int Start, int End) FindBracketsBackward(string s, int beforeIndex)
    {
        var pos = beforeIndex - 1;
        while (pos >= 0 && s[pos] == ' ') pos--;
        if (pos < 0 || s[pos] != '}')
            throw new InvalidOperationException("No closing bracket before index");

        var end = pos + 1;
        var depth = 1;
        pos--;
        while (pos >= 0)
        {
            if      (s[pos] == '}') depth++;
            else if (s[pos] == '{') { depth--; if (depth == 0) return (pos, end); }
            pos--;
        }
        throw new InvalidOperationException("Unmatched bracket");
    }

    // ── Structural transforms ─────────────────────────────────────────────

    /// <summary>
    /// Replaces <c>{num} over {den}</c> with <c>\frac{num}{den}</c>.
    /// Handles both lowercase <c>over</c> (HML) and HWPX's <c>OVER</c>
    /// (already normalized to lowercase by the token map).
    /// </summary>
    private static string ReplaceFrac(string s)
        => ReplaceInfixBinary(s, " over ", (num, den) => $@"\frac{{{num}}}{{{den}}}");

    /// <summary>
    /// Replaces <c>root {n} of {expr}</c> with <c>\sqrt[n]{expr}</c>.
    /// </summary>
    private static string ReplaceRootOf(string s)
    {
        const string rootKw = "root ";   // no leading space — checked separately
        const string ofKw   = " of ";
        while (true)
        {
            // Find `root ` either at position 0 or preceded by whitespace.
            // Previously the keyword was " root " which made position 0 unreachable
            // (single-equation input starting with `root {n} of {x}` was missed).
            var rootPos = -1;
            var scan = 0;
            while (scan < s.Length)
            {
                var p = s.IndexOf(rootKw, scan, StringComparison.Ordinal);
                if (p < 0) break;
                if (p == 0 || char.IsWhiteSpace(s[p - 1])) { rootPos = p; break; }
                scan = p + 1;
            }
            if (rootPos < 0) break;

            var ofPos = s.IndexOf(ofKw, rootPos + rootKw.Length, StringComparison.Ordinal);
            if (ofPos < 0) break;
            try
            {
                var (s1, e1) = FindBracketsForward(s, rootPos + rootKw.Length);
                var (s2, e2) = FindBracketsForward(s, ofPos + 1);
                var n    = s[(s1 + 1)..(e1 - 1)];
                var expr = s[(s2 + 1)..(e2 - 1)];
                s = s[..rootPos] + @"\sqrt[" + n + "]{" + expr + "}" + s[e2..];
            }
            catch (InvalidOperationException) { break; }
        }
        return s;
    }

    /// <summary>
    /// Infix binary operator: <c>A CHOOSE B</c> → <c>\binom{A}{B}</c>.
    /// Both operands may be a braced group or a single bare token.
    /// </summary>
    private static string ReplaceChoose(string s)
        => ReplaceInfixBinary(s, " CHOOSE ", (num, den) => $@"\binom{{{num}}}{{{den}}}");

    /// <summary>
    /// Infix binary operator: <c>A atop B</c> → <c>{A \atop B}</c>.
    /// Spec §1.2 ATOP is OVER without the fraction bar.
    /// </summary>
    private static string ReplaceAtop(string s)
        => ReplaceInfixBinary(s, " atop ", (num, den) => $@"{{{num} \atop {den}}}");

    /// <summary>
    /// Prefix binary operator: <c>binom A B</c> → <c>\binom{A}{B}</c>.
    /// Spec §1.2 shows CHOOSE and binom are equivalent, differing in syntax.
    /// </summary>
    private static string ReplaceBinomPrefix(string s)
    {
        const string keyword = "binom";
        var searchStart = 0;
        while (searchStart < s.Length)
        {
            // Match `binom` as a whole token — preceded by start/space and
            // followed by whitespace.
            var cursor = s.IndexOf(keyword, searchStart, StringComparison.Ordinal);
            if (cursor < 0) break;
            var afterKw = cursor + keyword.Length;
            var precededByBoundary = cursor == 0 || char.IsWhiteSpace(s[cursor - 1]);
            var followedByBoundary = afterKw < s.Length && char.IsWhiteSpace(s[afterKw]);
            if (!precededByBoundary || !followedByBoundary)
            {
                searchStart = cursor + 1;
                continue;
            }

            var (a1Start, a1End) = FindArgForward(s, afterKw);
            if (a1Start < 0) { searchStart = afterKw; continue; }
            var (a2Start, a2End) = FindArgForward(s, a1End);
            if (a2Start < 0) { searchStart = afterKw; continue; }

            var a1 = UnwrapBraces(s[a1Start..a1End]);
            var a2 = UnwrapBraces(s[a2Start..a2End]);
            s = s[..cursor] + $@"\binom{{{a1}}}{{{a2}}}" + s[a2End..];
            searchStart = 0;
        }
        return s;
    }

    /// <summary>
    /// Generic infix binary structural transform. <paramref name="keyword"/> must
    /// include surrounding whitespace (e.g. <c>" CHOOSE "</c>).
    /// </summary>
    private static string ReplaceInfixBinary(string s, string keyword, Func<string, string, string> format)
    {
        var searchStart = 0;
        while (searchStart < s.Length)
        {
            var cursor = s.IndexOf(keyword, searchStart, StringComparison.Ordinal);
            if (cursor < 0) break;
            var (lStart, lEnd) = FindArgBackward(s, cursor);
            var (rStart, rEnd) = FindArgForward(s, cursor + keyword.Length);
            if (lStart < 0 || rStart < 0)
            {
                searchStart = cursor + keyword.Length;
                continue;
            }
            var lhs = UnwrapBraces(s[lStart..lEnd]);
            var rhs = UnwrapBraces(s[rStart..rEnd]);
            s = s[..lStart] + format(lhs, rhs) + s[rEnd..];
            searchStart = 0;
        }
        return s;
    }

    /// <summary>
    /// Finds the argument immediately before <paramref name="beforeIndex"/>:
    /// a balanced <c>{ }</c> group if the prior non-space char is <c>}</c>,
    /// otherwise a single whitespace-delimited token. Returns <c>(-1, -1)</c>
    /// if no argument can be identified.
    /// </summary>
    private static (int Start, int End) FindArgBackward(string s, int beforeIndex)
    {
        var pos = beforeIndex - 1;
        while (pos >= 0 && char.IsWhiteSpace(s[pos])) pos--;
        if (pos < 0) return (-1, -1);

        if (s[pos] == '}')
        {
            var end = pos + 1;
            var depth = 1;
            pos--;
            while (pos >= 0)
            {
                if      (s[pos] == '}') depth++;
                else if (s[pos] == '{') { depth--; if (depth == 0) return (pos, end); }
                pos--;
            }
            return (-1, -1);
        }

        var tokenEnd = pos + 1;
        while (pos >= 0 && !char.IsWhiteSpace(s[pos])) pos--;
        return (pos + 1, tokenEnd);
    }

    /// <summary>
    /// Finds the argument immediately after <paramref name="fromIndex"/>:
    /// a balanced <c>{ }</c> group if the next non-space char is <c>{</c>,
    /// otherwise a single whitespace-delimited token.
    /// </summary>
    private static (int Start, int End) FindArgForward(string s, int fromIndex)
    {
        var pos = fromIndex;
        while (pos < s.Length && char.IsWhiteSpace(s[pos])) pos++;
        if (pos >= s.Length) return (-1, -1);

        if (s[pos] == '{')
        {
            var start = pos;
            var depth = 1;
            pos++;
            while (pos < s.Length)
            {
                if      (s[pos] == '{') depth++;
                else if (s[pos] == '}') { depth--; if (depth == 0) return (start, pos + 1); }
                pos++;
            }
            return (-1, -1);
        }

        var tokenStart = pos;
        while (pos < s.Length && !char.IsWhiteSpace(s[pos])) pos++;
        return (tokenStart, pos);
    }

    /// <summary>
    /// If <paramref name="arg"/> is wrapped in <c>{ }</c>, returns the inner
    /// content (trimmed). Otherwise returns the input unchanged.
    /// </summary>
    private static string UnwrapBraces(string arg)
    {
        arg = arg.Trim();
        if (arg.Length >= 2 && arg[0] == '{' && arg[^1] == '}')
            return arg[1..^1].Trim();
        return arg;
    }

    /// <summary>
    /// Replaces matrix HULK markers with LaTeX matrix environments.
    /// Tries to remove the wrapping <c>{ }</c> group (HML style) when available.
    /// </summary>
    private static string ReplaceAllMatrix(string s)
    {
        foreach (var (key, (begin, end, removeOuter)) in MatrixConvertMap)
        {
            while (true)
            {
                var cursor = s.IndexOf(key, StringComparison.Ordinal);
                if (cursor < 0) break;
                try
                {
                    var (eStart, eEnd) = FindBracketsForward(s, cursor);
                    var inner = s[(eStart + 1)..(eEnd - 1)]
                        .Replace("#",     @" \\ ")
                        .Replace("&amp;", "&");

                    if (removeOuter)
                    {
                        // Try to consume a surrounding { } group in HML convention
                        // `{matrix{rows}}`. Only whitespace is allowed between the
                        // outer `{` and the HULK marker — otherwise the nearest `{`
                        // going back would be an unrelated earlier group (e.g. the
                        // `{matrix}` of a prior \begin{matrix} emitted by this very
                        // pass), and consuming it would leave the HULK marker in
                        // place → infinite loop.
                        try
                        {
                            var p = cursor - 1;
                            while (p >= 0 && char.IsWhiteSpace(s[p])) p--;
                            if (p >= 0 && s[p] == '{')
                            {
                                var (bStart, bEnd) = FindBracketsForward(s, p);
                                s = s[..bStart] + begin + inner + end + s[bEnd..];
                                continue;
                            }
                        }
                        catch (InvalidOperationException) { /* no outer group — fall through */ }
                    }

                    s = s[..cursor] + begin + inner + end + s[eEnd..];
                }
                catch (InvalidOperationException) { break; }
            }
        }
        return s;
    }

    /// <summary>
    /// Replaces accent HULK markers (<c>hat</c>, <c>vec</c>, <c>bar</c>, …) with
    /// LaTeX accent commands.
    /// <para>
    /// HWPX-specific: does NOT attempt outer-group removal. The original hml-equation-parser
    /// uses <c>_findOutterBrackets</c>, which causes an infinite loop when HWPX double-brace
    /// notation (<c>hat {{x}}</c>) is present. Replacing the marker in-place is correct
    /// for both HML and HWPX formats.
    /// </para>
    /// </summary>
    private static string ReplaceAllBar(string s)
    {
        foreach (var (barKey, barCmd) in BarConvertMap)
        {
            while (true)
            {
                int cursor = s.IndexOf(barKey, StringComparison.Ordinal);
                if (cursor < 0) break;
                try
                {
                    var (eStart, eEnd) = FindBracketsForward(s, cursor);
                    string elem = s[eStart..eEnd];
                    // Replace: HULKXXX {arg} → \cmd{arg}
                    s = s[..cursor] + barCmd + elem + s[eEnd..];
                }
                catch (InvalidOperationException) { break; }
            }
        }
        return s;
    }

    /// <summary>
    /// Replaces OVERBRACE/UNDERBRACE markers with LaTeX brace commands.
    /// </summary>
    private static string ReplaceAllBrace(string s)
    {
        foreach (var (braceKey, braceCmd) in BraceConvertMap)
        {
            while (true)
            {
                int cursor = s.IndexOf(braceKey, StringComparison.Ordinal);
                if (cursor < 0) break;
                try
                {
                    var (s1, e1) = FindBracketsForward(s, cursor);
                    var (s2, e2) = FindBracketsForward(s, e1);
                    string elem1 = s[s1..e1];
                    string elem2 = s[s2..e2];
                    s = s[..cursor] + braceCmd + elem1 + "^" + elem2 + s[e2..];
                }
                catch (InvalidOperationException) { break; }
            }
        }
        return s;
    }

    // ── Regex helpers ─────────────────────────────────────────────────────

    [GeneratedRegex(@"REL\s+(\w+arrow)\s+\{([^}]*)\}\s+\{[^}]*\}", RegexOptions.IgnoreCase)]
    private static partial Regex RelArrowRegex();

    // Captures `BUILDREL <arrow> {upper}` in $1; the trailing `\s+\{[^}]*\}`
    // (the lower label) is outside the capture group so Replace("$1") drops it.
    // IgnoreCase intentionally off — `BUILDREL` is uppercase per spec §1.2.
    [GeneratedRegex(@"(BUILDREL\s+\S+\s+\{[^}]*\})\s+\{[^}]*\}")]
    private static partial Regex BuildrelDropLowerRegex();

    // Nth-root shorthand. Captures the degree (digits, optionally braced) so
    // `^3sqrt{x}` and `^{3} sqrt {x}` both rewrite to `root {3} of {x}`.
    // The trailing \b (word boundary) prevents matching substrings like
    // `^3sqrtX` where sqrt is part of a longer identifier.
    [GeneratedRegex(@"\^\s*\{?\s*(\d+)\s*\}?\s*sqrt\b")]
    private static partial Regex NthRootShorthandRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceRegex();
}
