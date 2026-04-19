namespace FieldCure.DocumentParsers.Tests;

/// <summary>
/// Snapshot tests for <see cref="HancomMathNormalizer.ToLaTeX"/>.
/// Inputs come from two HWPX fixtures (both in TestData/):
///   - hwpx-math-patterns.hwpx : user-authored canonical patterns
///     (Hancom spec §2.1–§2.5 examples + extra root-of variants). 7 scripts.
///   - hwpx-math-spec.hwpx     : full spec §1.2 basic-command reference
///     plus complete symbol inventory. 32 scripts.
///
/// Expected strings capture CURRENT behavior. Target renderings are in
/// todo/hwpx-math-spec_rev.pdf (not in-repo).
/// </summary>
[TestClass]
public class HancomMathNormalizerTests
{
    // =====================================================================
    // hwpx-math-patterns.hwpx — 7 canonical user scripts
    // =====================================================================

    [TestMethod]
    [DataRow(
        "10a^3 over b^2 times ~□ ~÷ b^3 over 2a =( 2a^2 over b )^3",
        @"\frac{10a^3}{b^2} \times □ ÷ \frac{b^3}{2a} = ( \frac{2a^2}{b} ) ^3",
        DisplayName = "Pattern1_FractionMulExp_OK")]
    [DataRow(
        "(A UNION B)^C` =` A^C INTER B^C",
        @"( A \bigcup B ) ^C = A^C \bigcap B^C",
        DisplayName = "Pattern2_DeMorgan_OK")]
    [DataRow(
        "int from 0 to 3 `^3sqrt{x^2 +1}dx",
        @"\int _ 0 ^ 3 \sqrt[ 3 ]{ x^2 +1 } dx",
        DisplayName = "Pattern3_IntegralInlineNthRoot_OK")]
    [DataRow(
        "int from 0 to 3 `root {3} of {x^2 +1} dx",
        @"\int _ 0 ^ 3 \sqrt[ 3 ]{ x^2 +1 } dx",
        DisplayName = "Pattern4_IntegralRootOfForm_OK")]
    [DataRow(
        "root {3} of {x}",
        @"\sqrt[ 3 ]{ x }",
        DisplayName = "Pattern5_RootOfAtStart_OK")]
    [DataRow(
        "X = bmatrix { 42 & 52 & 48 & 58 #\n4 & 5 & 4 & 3 }",
        @"X = \begin{bmatrix} 42 & 52 & 48 & 58 \\ 4 & 5 & 4 & 3 \end{bmatrix}",
        DisplayName = "Pattern6_BMatrix_OK")]
    [DataRow(
        "lim_N->inf 1 over N sum_n=1^N\nLEFT(SUM_k=1^n 1 over 2^k right)",
        // LIMITATION: space-less tokens (lim_N, sum_n, SUM_k) out of scope.
        // Fractions, LEFT/right all resolve though.
        @"lim_N->inf \frac{1}{N} sum_n=1^N \left ( SUM_k=1^n \frac{1}{2^k} \right )",
        DisplayName = "Pattern7_SpacelessLim_Partial")]
    public void Patterns_Snapshot(string input, string expected)
        => Assert.AreEqual(expected, HancomMathNormalizer.ToLaTeX(input));

    // =====================================================================
    // hwpx-math-spec.hwpx — 32 spec reference scripts
    // =====================================================================

    [TestMethod]
    [DataRow("2 times 5 = 10",
        @"2 \times 5 = 10",
        DisplayName = "Spec01_Times_OK")]
    [DataRow("1 over 2",
        @"\frac{1}{2}",
        DisplayName = "Spec02_OverBraceless_OK")]
    [DataRow("x atop y",
        @"{x \atop y}",
        DisplayName = "Spec03_Atop_OK")]
    [DataRow("sqrt 2, sqrt {2}, root {2} of {2}",
        @"\sqrt 2 , \sqrt { 2 } , \sqrt[ 2 ]{ 2 }",
        DisplayName = "Spec04_SqrtThreeForms_OK")]
    [DataRow("1 over 2 + {1} over {2} + {n um} over {den}",
        @"\frac{1}{2} + \frac{1}{2} + \frac{n um}{den}",
        DisplayName = "Spec05_OverBraceVariants_OK")]
    [DataRow("A REL <-> {+2} {-5} B # C BUILDREL <-> {+2} D",
        @"A \leftrightarrow { +2 } { -5 } B # C \leftrightarrow { +2 } D",
        DisplayName = "Spec06_RelBuildrel")]
    [DataRow("{a+b} over {a-b} bigg / {x+y} over {x-y}",
        // PARTIAL: both fractions convert; `bigg` (size modifier, spec §1.2)
        // still passes through as literal.
        @"\frac{a+b}{a-b} bigg / \frac{x+y}{x-y}",
        DisplayName = "Spec07_Bigg")]
    [DataRow("x times y &= z # z &= 10",
        @"x \times y & = z # z & = 10",
        DisplayName = "Spec08_AlignMarkers")]
    [DataRow("A CHOOSE B",
        @"\binom{A}{B}",
        DisplayName = "Spec09_Choose_OK")]
    [DataRow("binom A B",
        @"\binom{A}{B}",
        DisplayName = "Spec10_Binom_OK")]
    [DataRow(
        "Alpha, Beta, Gamma, Delta, Epsilon, Zeta, Eta, Theta, Iota, Kappa, Lambda, Mu, Nu, Xi, Omicron, Pi, Rho, Sigma, Tau, Upsilon, Phi, Chi, Psi, Omega",
        @"A , B , \Gamma , \Delta , E , Z , H , \Theta , I , K , \Lambda , M , N , \Xi , O , \Pi , P , \Sigma , T , \Upsilon , \Phi , X , \Psi , \Omega",
        DisplayName = "Spec11_GreekUppercase_OK")]
    [DataRow(
        "alpha, beta, gamma, delta, epsilon, zeta, eta, theta, iota, kappa, lambda, mu, nu, xi, omicron, pi, rho, sigma, tau, upsilon, phi, chi, psi, omega",
        @"\alpha , \beta , \gamma , \delta , \epsilon , \zeta , \eta , \theta , \iota , \kappa , \lambda , \mu , \nu , \xi , \omicron , \pi , \rho , \sigma , \tau , \upsilon , \phi , \chi , \psi , \omega",
        DisplayName = "Spec12_GreekLowercase_OK")]
    [DataRow(
        "ALEPH, HBAR, IMATH, JMATH, OHM, ELL, Liter, WP, IMAG, ANGSTROM, vartheta, varpi, varsigma, varupsilon, varphi, varepsilon",
        @"\aleph , \hbar , \imath , \jmath , \Omega , \ell , \ell , \wp , \Im , \AA , \vartheta , \varpi , \varsigma , \varUpsilon , \varphi , \varepsilon",
        DisplayName = "Spec13_MathLetters_OK")]
    [DataRow(
        "Sigma, PROD, PROD from x to y , PROD from x , PROD to y ,COPROD, COPROD from x to y , COPROD from x , COPROD to y ,\nINTER, INTER from x to y , INTER from x , INTER to y , CAP, SQCAP, \nSQCUP, OPLUS, OMINUS,\nOTIMES, ODOT, OSLASH,\nVEE, WEDGE, SUBSET,",
        @"\Sigma , \prod , \prod _ x ^ y , \prod _ x , \prod ^ y , \coprod , \coprod _ x ^ y , \coprod _ x , \coprod ^ y , \bigcap , \bigcap _ x ^ y , \bigcap _ x , \bigcap ^ y , \cap , \sqcap , \sqcup , \oplus , \ominus , \otimes , \odot , \oslash , \vee , \wedge , \subset ,",
        DisplayName = "Spec14_BigOpsFromTo_OK")]
    [DataRow(
        "SUPSET, SUBSETEQ, SUPSETEQ,\nIN, OWNS, notin,\nLEQ, GEQ, SQSUBSET,\nSQSUPSET, SQSUBSETEQ, SQSUPSETEQ,\n<<, >>, LLL,\n>>>, PREC, SUCC,\nUPLUS",
        @"\supset , \subseteq , \supseteq , \in , \owns , \notin , \leq , \geq , \sqsubset , \sqsupset , \sqsubseteq , \sqsupseteq , \ll , \gg , \lll , \ggg , \prec , \succ , \uplus",
        DisplayName = "Spec15_MoreRelations_OK")]
    [DataRow(
        "PLUSMINUS, MINUSPLUS, times,\nDIV, DIVIDE, CIRC, BULLET,\nDEG, AST, STAR,\nBIGCIRC, EMPTYSET, THEREFORE,\nBECAUSE, IDENTICAL, EXIST,\nneq, !=, DOTEQ, image, reimage, REIMAGE",
        @"\pm , \mp , \times , \div , \div , \circ , \bullet , ^\circ , \ast , \bigstar , \bigcirc , \emptyset , \therefore , \because , \equiv , \exists , \neq , \neq , \doteq , \Im , \Re , \Re",
        DisplayName = "Spec16_Arithmetic_OK")]
    [DataRow(
        "REIMAGE SIM APPROX\nSIMEQ CONG ==, EQUIV\nASYMP ISO DIAMOND\nDSUM FORALL prime\nPARTIAL inf LNOT\nPROPTO XOR TRIANGLED\nDAGGER DDAGGER",
        @"\Re \sim \approx \simeq \cong \equiv , \equiv \asymp \Bumpeq \diamond \dotplus \forall ' \partial \infty \lnot \propto \veebar \triangledown \dagger \ddagger",
        DisplayName = "Spec17_Relations_OK")]
    [DataRow(
        "larrow, rarrow, uparrow,\ndownarrow, LARROW, RARROW,\nUPARROW, DOWNARROW, udarrow,\nlrarrow, UDARROW, LRARROW,\nnwarrow, searrow, nearrow,\nswarrow, hookleft, hookright,\nmapsto, vert, VERT",
        @"\leftarrow , \rightarrow , \uparrow , \downarrow , \Leftarrow , \Rightarrow , \Uparrow , \Downarrow , \updownarrow , \leftrightarrow , \Updownarrow , \Leftrightarrow , \nwarrow , \searrow , \nearrow , \swarrow , \hookleftarrow , \hookrightarrow , \mapsto , \vert , \Vert",
        DisplayName = "Spec18_Arrows_OK")]
    [DataRow(
        "cdots, LDOTS, VDOTS,\nDDOTS, TRIANGLE, TRIANGLED,\nANGLE, MSANGLE, SANGLE,\nRTANGLE, VDASH, HLEFT,\nBOT, TOP, MODELS,\nLAPLACE, CENTIGRADE, FAHRENHEIT,\nLSLANT, RSLANT, att,\nhund, thou, well,\nbase, benzene",
        // PARTIAL: att, hund, thou, well, base, benzene unmapped (obscure per
        // spec §1.2.4.6 기타 기호 — ※/‰/‱/♯/△-variant/⬡ without direct LaTeX
        // primitives). Defer until observed in real docs.
        @"\cdots , \ldots , \vdots , \ddots , \triangle , \triangledown , \angle , \measuredangle , \sphericalangle , \sphericalangle , \vdash , \dashv , \bot , \top , \models , \mathcal{L} , ^{\circ}C , ^{\circ}F , \diagup , \diagdown , att , hund , thou , well , base , benzene",
        DisplayName = "Spec19_MiscSymbols_MostlyOK")]
    [DataRow(
        "sinh(x), cosh(x), arcsin(x), exp(x), \nmax(x,y), min(x,y),\ndet(A),\ngcd(x, y, z), mod(x, y)",
        @"\sinh ( x ) , \cosh ( x ) , \arcsin ( x ) , \exp ( x ) , \max ( x , y ) , \min ( x , y ) , \det ( A ) , \gcd ( x , y , z ) , \bmod ( x , y )",
        DisplayName = "Spec20_RomanFunctions_OK")]
    [DataRow(
        "vec {x}, dyad {x}, acute {x}, grave {x}, dot {x}, ddot {x}, under {x}, bar {x}, hat {x}, check {x}, arch {x}, tilde {x}, box {x+y}",
        @"\overrightarrow{ x } , \overleftrightarrow{ x } , \acute{ x } , \grave{ x } , \dot{ x } , \ddot{ x } , \underline { x } , \overline{ x } , \widehat{ x } , \check{ x } , \overset{\frown}{ x } , \widetilde{ x } , \boxed{ x+y }",
        DisplayName = "Spec21_Decorations_OK")]
    [DataRow("rm x, it x, bold x",
        // PARTIAL: rm/it/bold → \mathrm/\mathit/\mathbf. Without braces the
        // LaTeX output still only applies to one char (matches Hancom editor
        // degraded behavior per spec §1.1.1.1).
        @"\mathrm x , \mathit x , \mathbf x",
        DisplayName = "Spec22_FontSwitches_PartialOK")]
    [DataRow("y = lim _{x -> 0} {{1} over {x}}",
        @"y = \lim _ { x \rightarrow 0 } { \frac{1}{x} }",
        DisplayName = "Spec23_LimProperForm_OK")]
    [DataRow(
        "lim _{} { {x} over {a}} , lim _{a rarrow 0} {x^a}, lim _{ a->0} {x^a}, # \nLim _{} {x/a}, Lim _{ a->0} {x/a }, Lim _{ x->inf} {2x over x^2 }",
        // All lim/Lim/over convert. Remaining: `a->0` space-less still literal
        // (space-less out of scope).
        @"\lim _ { } { \frac{x}{a} } , \lim _ { a \rightarrow 0 } { x^a } , \lim _ { a->0 } { x^a } , # \lim _ { } { x/a } , \lim _ { a->0 } { x/a } , \lim _ { x->inf } { \frac{2x}{x^2} }",
        DisplayName = "Spec24_LimVariants_OK")]
    [DataRow(
        "COPROD _{x} ^{y} , COPROD from {x} to {y}, COPROD from x to y",
        @"\coprod _ { x } ^ { y } , \coprod _ { x } ^ { y } , \coprod _ x ^ y",
        DisplayName = "Spec25_CoprodEquivalents_OK")]
    [DataRow("rm 2H_2 O = 2H_2 + O_2",
        // PARTIAL: \mathrm applies to `2` only (next token). Same degraded
        // behavior the Hancom editor exhibits (spec §1.1.3 note). For full
        // chemical-formula fidelity user would write `rm {2H_2 O}`.
        @"\mathrm 2H_2 O = 2H_2 + O_2",
        DisplayName = "Spec26_Chemical_PartialOK")]
    [DataRow(
        "LONGDIV {b} {c} {a} , {LADDER{c&a&b#d&e&}} , {SLADDER{b&a&#c&d&}}",
        @"LONGDIV { b } { c } { a } , { LADDER { c & a & b#d & e & } } , { SLADDER { b & a & #c & d & } }",
        DisplayName = "Spec27_LongdivLadder")]
    [DataRow("cases {2x+y=4 # 3x-4y=-1}",
        @"\begin{cases} 2x+y=4 \\ 3x-4y=-1 \end{cases}",
        DisplayName = "Spec28_Cases_OK")]
    [DataRow(
        "REL LRARROW {a} {b} , REL LRARROW {a} {}, REL lrarrow {a} {b} , REL lrarrow {a} {} , REL RARROW {a} {b} ,#\nREL RARROW {a} {}, REL rarrow {a} {b}, REL rarrow {a} {}, REL LARROW {a} {b}, REL LARROW {a} {}#\nREL larrow {a} {b} , REL larrow {a} {}, REL EXARROW {a} {b} , REL EXARROW {a} {}",
        // LRARROW / lrarrow now → \xleftrightarrow (bidi). EXARROW (error-bar
        // arrow, spec-specific) still falls through to \xrightarrow — no
        // standard LaTeX analogue without a package.
        "\\xleftrightarrow { a } , \\xleftrightarrow { a } , \\xleftrightarrow { a } , \\xleftrightarrow { a } , \\xrightarrow { a } , # \\xrightarrow { a } , \\xrightarrow { a } , \\xrightarrow { a } , \\xleftarrow { a } , \\xleftarrow { a } # \\xleftarrow { a } , \\xleftarrow { a } , \\xrightarrow { a } , \\xrightarrow { a }",
        DisplayName = "Spec29_RelArrows_MostlyOK")]
    [DataRow(
        "LEFT (  x RIGHT ), LEFT [ x RIGHT ], LEFT { x RIGHT }, LEFT < x RIGHT >, LEFT |  x RIGHT | #\n LEFT DLINE x RIGHT DLINE, LCEIL x RCEIL, LFLOOR x RFLOOR, OVERBRACE {x+y} {b}, UNDERBRACE {a} {x+y}",
        @"\left ( x \right ) , \left [ x \right ] , \left \{ x \right \} , \left < x \right > , \left | x \right | # \left DLINE x \right DLINE , LCEIL x RCEIL , LFLOOR x RFLOOR , \overbrace{ x+y }^{ b } , \underbrace{ a }^{ x+y }",
        DisplayName = "Spec30_LeftRightDelims")]
    [DataRow("pile{ abcd#b }\n# lpile{abcd #b} \n# rpile{abcd #b}",
        // OK: all three pile variants now convert via HULKPILE → \begin{matrix}.
        // The `#` separators between the three piles are outside any matrix
        // body so they stay literal (not converted to `\\`).
        // lpile/rpile alignment (left/right) is lost — LaTeX's matrix is always
        // centred. Acceptable for LLM extraction.
        @"\begin{matrix} abcd \\ b \end{matrix} # \begin{matrix} abcd \\ b \end{matrix} # \begin{matrix} abcd \\ b \end{matrix}",
        DisplayName = "Spec31_Pile_OK")]
    [DataRow(
        "{matrix{a&b#c&d}}, {pmatrix{a&b#c&d}}, {dmatrix{a&b#c&d}}, {bmatrix{a&b#c&d}}",
        @"\begin{matrix} a & b \\ c & d \end{matrix} , \begin{pmatrix} a & b \\ c & d \end{pmatrix} , \begin{vmatrix} a & b \\ c & d \end{vmatrix} , \begin{bmatrix} a & b \\ c & d \end{bmatrix}",
        DisplayName = "Spec32_MatrixFourVariants_OK")]
    public void Spec_Snapshot(string input, string expected)
        => Assert.AreEqual(expected, HancomMathNormalizer.ToLaTeX(input));
}
