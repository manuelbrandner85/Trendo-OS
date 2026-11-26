# aufgaben-libreoffice

## 1. Löse nach x: 5x − 3 = 2x + 9

*Gleichung lösen*

**Lösungsweg**

1. Alles auf eine Seite bringen, ausmultiplizieren und zusammenfassen  
   `5x − 3 = 2x + 9  ⇔  3x − 12 = 0`
2. +12 auf beiden Seiten  
   `3x = 12`
3. Beide Seiten durch 3 teilen  
   `x = 12/3 = 4`

**Ergebnis:** x = 4

$$x_{1} = 4$$

**Probe**

- ✓ x = 4 einsetzen: links 17, rechts 17 – stimmt (exakt).

## 2. Löse nach x: x² + 2x − 15 = 0

*Gleichung lösen*

**Lösungsweg**

1. p-q-Formel mit p = 2 und q = −15: x = −p/2 ± √((p/2)² − q)  
   `x = −1 ± √((1)² − (−15)) = −1 ± √(16)`
2. √(16) = 4 – zwei Lösungen  
   `x₁ = −1 + 4 = 3;  x₂ = −1 − 4 = −5`

**Ergebnis:** x₁ = −5;  x₂ = 3

$$x_{1} = -5,\quad x_{2} = 3$$

**Probe**

- ✓ x = −5 einsetzen: links 0, rechts 0 – stimmt (exakt).
- ✓ x = 3 einsetzen: links 0, rechts 0 – stimmt (exakt).

## 3. Löse das Gleichungssystem

*Gleichungssystem*

**Lösungsweg**

1. Jede Gleichung ordnen: Variablen links, Zahl rechts  
   `I: x + y = 10 | II: x − y = 2`
2. x aus den anderen Gleichungen entfernen (Additionsverfahren): II − I  
   `I: x + y = 10 | II: −2y = −8`
3. Gleichung II durch −2 teilen, damit vor y eine 1 steht  
   `I: x + y = 10 | II: y = 4`
4. y aus den anderen Gleichungen entfernen (Additionsverfahren): I − II  
   `I: x = 6 | II: y = 4`
5. Lösung ablesen  
   `x = 6;  y = 4`

**Ergebnis:** x = 6;  y = 4

$$x = 6,\quad y = 4$$

**Probe**

- ✓ Gleichung I (x + y = 10) mit der Lösung: links 10, rechts 10.
- ✓ Gleichung II (x − y = 2) mit der Lösung: links 2, rechts 2.

## 4. Berechne: 2/3 + 3/4

*Bruchrechnen*

**Lösungsweg**

1. Hauptnenner finden: kgV(3, 4) = 12; beide Brüche darauf erweitern  
   `2/3 + 3/4 = 8/12 + 9/12`
2. Zähler addieren  
   `= 17/12`

**Ergebnis:** 17/12 = 1 5/12 ≈ 1,4166666667

$$\frac{17}{12}$$

**Probe**

- ✓ Gegenrechnung mit dem Taschenrechner (Gleitkomma): 1,4166666667 – Bruch 17/12 ergibt 1,4166666667.

## 5. Faktorisiere: x² − 16

*Faktorisieren*

**Lösungsweg**

1. 3. binomische Formel rückwärts: a² − b² = (a + b)(a − b)  
   `x² − 16 = (x + 4)(x − 4)`
2. Ergebnis  
   `x² − 16 = (x + 4)(x − 4)`

**Ergebnis:** (x + 4)(x − 4)

$$\left(x + 4\right) \cdot \left(x - 4\right)$$

**Probe**

- ✓ Ausmultipliziert ergibt (x + 4)(x − 4) wieder x² − 16 – genau der Ausgangsterm.

## 6. Integriere: ∫ (4x³ − 2x) dx

*Integrieren*

**Lösungsweg**

1. Summenregel: jedes Glied einzeln integrieren  
   `∫ (4x³ − 2x) dx = ∫ 4x³ dx − ∫ 2x dx`
2. Faktorregel: der Faktor 4 kommt vor das Integral  
   `∫ 4x³ dx = 4 · ∫ x³ dx`
3. Potenzregel: ∫ uⁿ du = uⁿ⁺¹/(n + 1) mit n = 3  
   `∫ x³ dx = x⁴/4`
4. Faktorregel: der Faktor −2 kommt vor das Integral  
   `∫ −2x dx = −2 · ∫ x dx`
5. Potenzregel: ∫ xⁿ dx = xⁿ⁺¹/(n + 1) mit n = 1  
   `∫ x dx = x²/2`
6. Stammfunktion (C ist eine beliebige Konstante)  
   `∫ (4x³ − 2x) dx = x⁴ − x² + C`

**Ergebnis:** ∫ (4x³ − 2x) dx = x⁴ − x² + C

$$\int 4 \cdot x^{3} - 2 \cdot x\,dx = x^{4} - x^{2} + C$$

**Probe**

- ✓ Probe durch Ableiten: F' stimmt an 6 Stellen mit f überein – bei x = 0,7: F' ≈ −0,028, f = −0,028.

## 7. Leite ab: f(x) = x²·e^x

*Ableiten*

**Lösungsweg**

1. Produktregel: (u·v)' = u'·v + u·v' mit u = x², v = e^x  
   `(x²·e^x)' = x²·e^x + 2x·e^x`
2. Potenzregel: (xⁿ)' = n·xⁿ⁻¹ mit n = 2  
   `(x²)' = 2x`
3. e-Funktion: (eᵘ)' = eᵘ·u'  
   `(e^x)' = e^x`
4. Ableitung von x  
   `(x)' = 1`
5. Zusammenfassen  
   `f'(x) = x²·e^x + 2x·e^x`

**Ergebnis:** f'(x) = x²·e^x + 2x·e^x

$$f'(x) = x^{2} \cdot e^{x} + 2 \cdot x \cdot e^{x}$$

**Probe**

- ✓ Gegenrechnung mit dem Differenzenquotienten an 6 Stellen – bei x = 0,7: Differenzenquotient 3,805993, Ableitung 3,805993.

---
Gerechnet mit dem Rechenkern von Trendo OS (exakt mit Brüchen, Probe unabhängig vom Lösungsweg).
