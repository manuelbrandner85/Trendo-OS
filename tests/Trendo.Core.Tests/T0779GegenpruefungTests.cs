using Trendo.Core.Wissen;
using Xunit;

namespace Trendo.Core.Tests;

/// <summary>T-0779 Gegenprüfung: Prüfungen, die neue Hilfen brauchen (übersetzen auf dem alten Stand nicht).</summary>
public class T0779GegenpruefungTests
{
    [Fact(DisplayName = "T-0779 H1: Lückenregel – Erwartung für die Browser-Prüffälle (data-tq-rules) kommt aus derselben Regel wie Trendo")]
    public void H1_rule_cases() => Assert.Equal("00000111", QuizHtml.RuleExpectation);

    [Fact(DisplayName = "T-0779 Niedrig: Quiz im Kurs übernimmt die Bestehensgrenze des Kurses")]
    public void Course_threshold()
    {
        var k = new Kurs { Bestehen = 75 }; var q = new Quiz { Bestehen = 60 };
        Assert.Equal(75, KursKit.MitKursGrenze(q, k).Bestehen); Assert.Equal(60, q.Bestehen);
    }
}
