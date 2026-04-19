namespace FieldCure.DocumentParsers.Tests;

/// <summary>
/// Snapshot tests for <see cref="HancomMathNormalizer.ToLaTeX"/>.
/// Inputs come from two HWPX fixtures (both in TestData/):
///   - hwpx-math-patterns.hwpx : user-authored canonical patterns
///     (Hancom spec §2.1–§2.5 examples + extra root-of variants). 7 scripts.
///   - hwpx-math-spec.hwpx     : full spec §1.2 basic-command reference
///     plus complete symbol inventory. 32 scripts.
///
/// Expected strings capture CURRENT behavior. Many entries are known-broken
/// (marked BUG/LIMITATION). The TARGET rendering for each spec script is in
/// todo/hwpx-math-spec_rev.pdf (not in-repo). When Phase 1a/1b patches land,
/// the affected snapshots must be updated to the new output.
/// </summary>
[TestClass]
public class HancomMathNormalizerTests
{
    // =====================================================================
    // hwpx-math-patterns.hwpx — 7 canonical user scripts
    // =====================================================================

    [DataTestMethod]
    [DataRow(
        "10a^3 over b^2 times ~□ ~÷ b^3 over 2a =( 2a^2 over b )^3",
        // BUG: `over` without {braces} is not converted.
        @"10a^3 over b^2 \times □ ÷ b^3 over 2a =( 2a^2 over b )^3",
        DisplayName = "Pattern1_FractionMulExp")]
    [DataRow(
        "(A UNION B)^C` =` A^C INTER B^C",
        // BUG: UNION/INTER missing from ConvertMap (Phase 1a adds).
        @"(A UNION B)^C = A^C INTER B^C",
        DisplayName = "Pattern2_DeMorgan")]
    [DataRow(
        "int from 0 to 3 `^3sqrt{x^2 +1}dx",
        // BUG: from/to not mapped (Phase 1a). ^3sqrt glued token (Phase 1b pre-pass).
        @"\int from 0 to 3 ^3sqrt { x^2 +1 } dx",
        DisplayName = "Pattern3_IntegralInlineNthRoot")]
    [DataRow(
        "int from 0 to 3 `root {3} of {x^2 +1} dx",
        // BUG: from/to not mapped. `\sqrt[ 3 ]` has stray spaces (LaTeX tolerant).
        @"\int from 0 to 3 \sqrt[ 3 ]{ x^2 +1 } dx",
        DisplayName = "Pattern4_IntegralRootOfForm")]
    [DataRow(
        "root {3} of {x}",
        // BUG: ReplaceRootOf searches for " root " (leading space), misses pos 0.
        @"root { 3 } of { x }",
        DisplayName = "Pattern5_RootOfAtStart")]
    [DataRow(
        "X = bmatrix { 42 & 52 & 48 & 58 #\n4 & 5 & 4 & 3 }",
        @"X = \begin{bmatrix} 42 & 52 & 48 & 58 \\ 4 & 5 & 4 & 3 \end{bmatrix}",
        DisplayName = "Pattern6_BMatrix")]
    [DataRow(
        "lim_N->inf 1 over N sum_n=1^N\nLEFT(SUM_k=1^n 1 over 2^k right)",
        // LIMITATION: space-less compound tokens out of scope.
        "lim_N->inf 1 over N sum_n=1^N\nLEFT(SUM_k=1^n 1 over 2^k right)",
        DisplayName = "Pattern7_SpacelessLim")]
    public void Patterns_Snapshot(string input, string expected)
        => Assert.AreEqual(expected, HancomMathNormalizer.ToLaTeX(input));

    // =====================================================================
    // hwpx-math-spec.hwpx — 32 spec reference scripts
    // =====================================================================

    [DataTestMethod]
    [DataRow("2 times 5 = 10",
        @"2 \times 5 = 10",
        DisplayName = "Spec01_Times_OK")]
    [DataRow("1 over 2",
        // BUG: brace-less `over` unconverted.
        "1 over 2",
        DisplayName = "Spec02_OverBraceless")]
    [DataRow("x atop y",
        // BUG: ATOP (spec §1.2 basic cmd) missing from ConvertMap.
        "x atop y",
        DisplayName = "Spec03_Atop")]
    [DataRow("sqrt 2, sqrt {2}, root {2} of {2}",
        // OK: all 3 sqrt forms convert (inline arg, braced arg, root..of).
        @"\sqrt 2, \sqrt { 2 } , \sqrt[ 2 ]{ 2 }",
        DisplayName = "Spec04_SqrtThreeForms_OK")]
    [DataRow("1 over 2 + {1} over {2} + {n um} over {den}",
        // BUG: ReplaceFrac breaks out of its loop on the first brace-less `over`,
        // so even the later braced forms don't get converted. Two bugs stacked:
        // (1) brace-less `over` unsupported, (2) failure aborts subsequent passes.
        @"1 over 2 + { 1 } over { 2 } + { n um } over { den }",
        DisplayName = "Spec05_OverBraceVariants")]
    [DataRow("A REL <-> {+2} {-5} B # C BUILDREL <-> {+2} D",
        // PARTIAL: both REL and BUILDREL map to \leftrightarrow (both arms kept).
        // TODO: BUILDREL should drop the lower arm (spec §1.2).
        @"A \leftrightarrow { +2 } { -5 } B # C \leftrightarrow { +2 } D",
        DisplayName = "Spec06_RelBuildrel")]
    [DataRow("{a+b} over {a-b} bigg / {x+y} over {x-y}",
        // PARTIAL: both fractions convert; `bigg` passes through as literal.
        @"\frac{ a+b } { a-b } bigg / \frac{ x+y } { x-y }",
        DisplayName = "Spec07_Bigg")]
    [DataRow("x times y &= z # z &= 10",
        // PARTIAL: times OK; `&` padded but kept; `#` literal (only matrix path
        // converts #→\\ inside matrix bodies).
        @"x \times y & = z # z & = 10",
        DisplayName = "Spec08_AlignMarkers")]
    [DataRow("A CHOOSE B",
        // BUG: CHOOSE missing from ConvertMap. Spec: (A/B) binomial form.
        "A CHOOSE B",
        DisplayName = "Spec09_Choose")]
    [DataRow("binom A B",
        // BUG: lowercase `binom` missing. Spec: same as CHOOSE.
        "binom A B",
        DisplayName = "Spec10_Binom")]
    [DataRow(
        "Alpha, Beta, Gamma, Delta, Epsilon, Zeta, Eta, Theta, Iota, Kappa, Lambda, Mu, Nu, Xi, Omicron, Pi, Rho, Sigma, Tau, Upsilon, Phi, Chi, Psi, Omega",
        // BUG: Pascal-case Greek unmapped (map has ALL-CAPS). Commas also glue.
        "Alpha, Beta, Gamma, Delta, Epsilon, Zeta, Eta, Theta, Iota, Kappa, Lambda, Mu, Nu, Xi, Omicron, Pi, Rho, Sigma, Tau, Upsilon, Phi, Chi, Psi, Omega",
        DisplayName = "Spec11_GreekUppercase")]
    [DataRow(
        "alpha, beta, gamma, delta, epsilon, zeta, eta, theta, iota, kappa, lambda, mu, nu, xi, omicron, pi, rho, sigma, tau, upsilon, phi, chi, psi, omega",
        // PARTIAL: only trailing \omega matches (no comma). Rest are `alpha,` etc.
        @"alpha, beta, gamma, delta, epsilon, zeta, eta, theta, iota, kappa, lambda, mu, nu, xi, omicron, pi, rho, sigma, tau, upsilon, phi, chi, psi, \omega",
        DisplayName = "Spec12_GreekLowercase")]
    [DataRow(
        "ALEPH, HBAR, IMATH, JMATH, OHM, ELL, Liter, WP, IMAG, ANGSTROM, vartheta, varpi, varsigma, varupsilon, varphi, varepsilon",
        // BUG: math-letter family unmapped + comma-gluing.
        "ALEPH, HBAR, IMATH, JMATH, OHM, ELL, Liter, WP, IMAG, ANGSTROM, vartheta, varpi, varsigma, varupsilon, varphi, varepsilon",
        DisplayName = "Spec13_MathLetters")]
    [DataRow(
        "Sigma, PROD, PROD from x to y , PROD from x , PROD to y ,COPROD, COPROD from x to y , COPROD from x , COPROD to y ,\nINTER, INTER from x to y , INTER from x , INTER to y , CAP, SQCAP, \nSQCUP, OPLUS, OMINUS,\nOTIMES, ODOT, OSLASH,\nVEE, WEDGE, SUBSET,",
        "Sigma, PROD, PROD from x to y , PROD from x , PROD to y ,COPROD, COPROD from x to y , COPROD from x , COPROD to y ,\nINTER, INTER from x to y , INTER from x , INTER to y , CAP, SQCAP, SQCUP, OPLUS, OMINUS,\nOTIMES, ODOT, OSLASH,\nVEE, WEDGE, SUBSET,",
        DisplayName = "Spec14_BigOpsFromTo")]
    [DataRow(
        "SUPSET, SUBSETEQ, SUPSETEQ,\nIN, OWNS, notin,\nLEQ, GEQ, SQSUBSET,\nSQSUPSET, SQSUBSETEQ, SQSUPSETEQ,\n<<, >>, LLL,\n>>>, PREC, SUCC,\nUPLUS",
        "SUPSET, SUBSETEQ, SUPSETEQ,\nIN, OWNS, notin,\nLEQ, GEQ, SQSUBSET,\nSQSUPSET, SQSUBSETEQ, SQSUPSETEQ,\n<<, >>, LLL,\n>>>, PREC, SUCC,\nUPLUS",
        DisplayName = "Spec15_MoreRelations")]
    [DataRow(
        "PLUSMINUS, MINUSPLUS, times,\nDIV, DIVIDE, CIRC, BULLET,\nDEG, AST, STAR,\nBIGCIRC, EMPTYSET, THEREFORE,\nBECAUSE, IDENTICAL, EXIST,\nneq, !=, DOTEQ, image, reimage, REIMAGE",
        "PLUSMINUS, MINUSPLUS, times,\nDIV, DIVIDE, CIRC, BULLET,\nDEG, AST, STAR,\nBIGCIRC, EMPTYSET, THEREFORE,\nBECAUSE, IDENTICAL, EXIST,\nneq, !=, DOTEQ, image, reimage, REIMAGE",
        DisplayName = "Spec16_Arithmetic")]
    [DataRow(
        "REIMAGE SIM APPROX\nSIMEQ CONG ==, EQUIV\nASYMP ISO DIAMOND\nDSUM FORALL prime\nPARTIAL inf LNOT\nPROPTO XOR TRIANGLED\nDAGGER DDAGGER",
        // PARTIAL: space-separated variant lets several relations convert
        // (SIM, CONG, ISO→Bumpeq, FORALL, inf, XOR→veebar, DDAGGER).
        // BUG: newline-not-split keeps `APPROX\nSIMEQ` glued.
        "REIMAGE \\sim APPROX\nSIMEQ \\cong ==, EQUIV\nASYMP \\Bumpeq DIAMOND\nDSUM \\forall prime\nPARTIAL \\infty LNOT\nPROPTO \\veebar TRIANGLED\nDAGGER \\ddagger",
        DisplayName = "Spec17_Relations_PartialConvert")]
    [DataRow(
        "larrow, rarrow, uparrow,\ndownarrow, LARROW, RARROW,\nUPARROW, DOWNARROW, udarrow,\nlrarrow, UDARROW, LRARROW,\nnwarrow, searrow, nearrow,\nswarrow, hookleft, hookright,\nmapsto, vert, VERT",
        "larrow, rarrow, uparrow,\ndownarrow, LARROW, RARROW,\nUPARROW, DOWNARROW, udarrow,\nlrarrow, UDARROW, LRARROW,\nnwarrow, searrow, nearrow,\nswarrow, hookleft, hookright,\nmapsto, vert, VERT",
        DisplayName = "Spec18_Arrows")]
    [DataRow(
        "cdots, LDOTS, VDOTS,\nDDOTS, TRIANGLE, TRIANGLED,\nANGLE, MSANGLE, SANGLE,\nRTANGLE, VDASH, HLEFT,\nBOT, TOP, MODELS,\nLAPLACE, CENTIGRADE, FAHRENHEIT,\nLSLANT, RSLANT, att,\nhund, thou, well,\nbase, benzene",
        "cdots, LDOTS, VDOTS,\nDDOTS, TRIANGLE, TRIANGLED,\nANGLE, MSANGLE, SANGLE,\nRTANGLE, VDASH, HLEFT,\nBOT, TOP, MODELS,\nLAPLACE, CENTIGRADE, FAHRENHEIT,\nLSLANT, RSLANT, att,\nhund, thou, well,\nbase, benzene",
        DisplayName = "Spec19_MiscSymbols")]
    [DataRow(
        "sinh(x), cosh(x), arcsin(x), exp(x), \nmax(x,y), min(x,y),\ndet(A),\ngcd(x, y, z), mod(x, y)",
        // BUG: spec §1.1.3 roman functions mostly missing. `sinh(x)` glued token.
        "sinh(x), cosh(x), arcsin(x), exp(x), max(x,y), min(x,y),\ndet(A),\ngcd(x, y, z), mod(x, y)",
        DisplayName = "Spec20_RomanFunctions")]
    [DataRow(
        "vec {x}, dyad {x}, acute {x}, grave {x}, dot {x}, ddot {x}, under {x}, bar {x}, hat {x}, check {x}, arch {x}, tilde {x}, box {x+y}",
        // OK (almost): all decorations work except `box` (lowercase; map has BOX uppercase).
        @"\overrightarrow{ x } , \overleftrightarrow{ x } , \acute{ x } , \grave{ x } , \dot{ x } , \ddot{ x } , \underline { x } , \overline{ x } , \widehat{ x } , \check{ x } , \overset{\frown}{ x } , \widetilde{ x } , box { x+y }",
        DisplayName = "Spec21_Decorations")]
    [DataRow("rm x, it x, bold x",
        // BUG: spec §1.1.1.1 font switches missing.
        "rm x, it x, bold x",
        DisplayName = "Spec22_FontSwitches")]
    [DataRow("y = lim _{x -> 0} {{1} over {x}}",
        // OK: spec §1.2 lim example converts correctly.
        @"y = \lim _ { x \rightarrow 0 } { \frac{ 1 } { x } }",
        DisplayName = "Spec23_LimProperForm_OK")]
    [DataRow(
        "lim _{} { {x} over {a}} , lim _{a rarrow 0} {x^a}, lim _{ a->0} {x^a}, # \nLim _{} {x/a}, Lim _{ a->0} {x/a }, Lim _{ x->inf} {2x over x^2 }",
        // PARTIAL: `lim` → \lim works, `a rarrow 0` also (rarrow→\rightarrow).
        // BUG: `a->0` glued inside `_{ a->0 }` stays literal (space-less `->` not split).
        // BUG: The first `Lim` after `# \n` stays capital — it's tokenized as
        //       `\nLim` (newline glued) so map lookup fails. Subsequent `Lim`s
        //       without leading \n convert to \lim normally.
        @"\lim _ { } { \frac{ x } { a } } , \lim _ { a \rightarrow 0 } { x^a } , \lim _ { a->0 } { x^a } , # Lim _ { } { x/a } , \lim _ { a->0 } { x/a } , \lim _ { x->inf } { 2x over x^2 }",
        DisplayName = "Spec24_LimVariants")]
    [DataRow(
        "COPROD _{x} ^{y} , COPROD from {x} to {y}, COPROD from x to y",
        // BUG: COPROD unmapped. Three forms should all render equivalently per spec.
        @"COPROD _ { x } ^ { y } , COPROD from { x } to { y } , COPROD from x to y",
        DisplayName = "Spec25_CoprodEquivalents")]
    [DataRow("rm 2H_2 O = 2H_2 + O_2",
        // BUG: `rm` font switch unmapped (spec §1.1.3 chemical formula example).
        "rm 2H_2 O = 2H_2 + O_2",
        DisplayName = "Spec26_Chemical")]
    [DataRow(
        "LONGDIV {b} {c} {a} , {LADDER{c&a&b#d&e&}} , {SLADDER{b&a&#c&d&}}",
        // BUG: LONGDIV/LADDER/SLADDER all unmapped. Note `b#d` inside {} is NOT a
        // matrix body, so `#` stays literal (matrix path wasn't entered).
        @"LONGDIV { b } { c } { a } , { LADDER { c & a & b#d & e & } } , { SLADDER { b & a & #c & d & } }",
        DisplayName = "Spec27_LongdivLadder")]
    [DataRow("cases {2x+y=4 # 3x-4y=-1}",
        // OK: CASES converts correctly via HULKCASE path.
        @"\begin{cases} 2x+y=4 \\ 3x-4y=-1 \end{cases}",
        DisplayName = "Spec28_Cases_OK")]
    [DataRow(
        "REL LRARROW {a} {b} , REL LRARROW {a} {}, REL lrarrow {a} {b} , REL lrarrow {a} {} , REL RARROW {a} {b} ,#\nREL RARROW {a} {}, REL rarrow {a} {b}, REL rarrow {a} {}, REL LARROW {a} {b}, REL LARROW {a} {}#\nREL larrow {a} {b} , REL larrow {a} {}, REL EXARROW {a} {b} , REL EXARROW {a} {}",
        // PARTIAL: RelArrowRegex converts most REL forms but drops the second {}
        // argument (lower label).
        // BUG: LRARROW→\xrightarrow (should be bidi); regex only distinguishes
        //       larrow vs others by substring.
        // BUG: EXARROW also falls through to \xrightarrow (the regex keyword
        //       group matches any `\w+arrow`).
        "\\xrightarrow { a } , \\xrightarrow { a } , \\xrightarrow { a } , \\xrightarrow { a } , \\xrightarrow { a } ,#\n\\xrightarrow { a } , \\xrightarrow { a } , \\xrightarrow { a } , \\xleftarrow { a } , \\xleftarrow { a } #\n\\xleftarrow { a } , \\xleftarrow { a } , \\xrightarrow { a } , \\xrightarrow { a }",
        DisplayName = "Spec29_RelArrows")]
    [DataRow(
        "LEFT (  x RIGHT ), LEFT [ x RIGHT ], LEFT { x RIGHT }, LEFT < x RIGHT >, LEFT |  x RIGHT | #\n LEFT DLINE x RIGHT DLINE, LCEIL x RCEIL, LFLOOR x RFLOOR, OVERBRACE {x+y} {b}, UNDERBRACE {a} {x+y}",
        // PARTIAL: ( [ { < | delimiters all work (`\left \{` / `\right \}` special-cased).
        // BUG: DLINE, LCEIL/RCEIL, LFLOOR/RFLOOR unmapped.
        // OK: OVERBRACE/UNDERBRACE work via HULK paths.
        @"\left ( x \right ), \left [ x \right ], \left \{ x \right \} , \left < x \right >, \left | x \right | # \left DLINE x \right DLINE, LCEIL x RCEIL, LFLOOR x RFLOOR, \overbrace{ x+y }^{ b } , \underbrace{ a }^{ x+y }",
        DisplayName = "Spec30_LeftRightDelims")]
    [DataRow("pile{ abcd#b }\n# lpile{abcd #b} \n# rpile{abcd #b}",
        // BUG: pile/lpile/rpile unmapped (spec §1.2).
        "pile { abcd#b } # lpile { abcd #b } # rpile { abcd #b }",
        DisplayName = "Spec31_Pile")]
    [DataRow(
        "{matrix{a&b#c&d}}, {pmatrix{a&b#c&d}}, {dmatrix{a&b#c&d}}, {bmatrix{a&b#c&d}}",
        // OK: all 4 matrix variants convert correctly.
        @"\begin{matrix} a & b \\ c & d \end{matrix} , \begin{pmatrix} a & b \\ c & d \end{pmatrix} , \begin{vmatrix} a & b \\ c & d \end{vmatrix} , \begin{bmatrix} a & b \\ c & d \end{bmatrix}",
        DisplayName = "Spec32_MatrixFourVariants_OK")]
    public void Spec_Snapshot(string input, string expected)
        => Assert.AreEqual(expected, HancomMathNormalizer.ToLaTeX(input));
}
