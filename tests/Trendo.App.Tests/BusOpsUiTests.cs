using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Trendo.App;
using Xunit;
namespace Trendo.App.Tests;

public class W5_Bus_Ops_Ui
{
    [AvaloniaFact(DisplayName = "BUS-017/018/019/020/023 (UI): Kontakt fällig, Mitglied mit Tarif, GiroCode, Nachbestellen, Anfrage lösen")]
    public void Ops()
    {
        var (w, _, _) = Ui.Open();
        w.Show("biz-betrieb");
        w.Find<TextBox>("op-c-name").Text = "Anna"; w.Find<TextBox>("op-c-next").Text = DateTime.Today.ToString("dd.MM.yyyy"); w.Find<Button>("op-c-add").Press();
        Assert.Contains("Anna", w.Find<TextBlock>("op-due").Text);
        w.Find<TextBox>("op-t-name").Text = "Basis"; w.Find<TextBox>("op-t-price").Text = "10"; w.Find<Button>("op-t-add").Press();
        w.Find<TextBox>("op-m-name").Text = "Ben"; w.Find<Button>("op-m-add").Press();
        Assert.Contains("10,00", w.Find<TextBlock>("op-mrr").Text!.Replace('.', ','));
        w.Find<TextBox>("op-g-name").Text = "Ich"; w.Find<TextBox>("op-g-iban").Text = "DE89370400440532013000"; w.Find<TextBox>("op-g-amount").Text = "5"; w.Find<Button>("op-g-make").Press();
        Assert.StartsWith("BCD", w.Find<SelectableTextBlock>("op-g-out").Text);
        w.Find<TextBox>("op-p-name").Text = "Tasse"; w.Find<TextBox>("op-p-stock").Text = "1"; w.Find<TextBox>("op-p-lead").Text = "5"; w.Find<TextBox>("op-p-daily").Text = "1"; w.Find<Button>("op-p-add").Press();
        Assert.Contains("nachbestellen", w.Find<TextBlock>("op-p-row-0").Text);
        w.Find<TextBox>("op-k-title").Text = "Wo ist mein Paket?"; w.Find<Button>("op-k-add").Press();
        w.Find<TextBox>("op-k-ans-0").Text = "Sendungsnummer in der Mail."; w.Find<Button>("op-k-solve-0").Press();
        Assert.Equal("gelöst", w.BizTickets.All()[0].State);
        w.Close();
    }
}
