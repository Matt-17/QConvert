using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace QConvert;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var assembly = typeof(AboutWindow).Assembly;
        var name = assembly.GetName();

        VersionText.Text = DisplayVersion(assembly, name);
        AuthorText.Text = AttributeValue<AssemblyCompanyAttribute>(assembly, attribute => attribute.Company) ?? "Code-iX";
        CompanyText.Text = AttributeValue<AssemblyCompanyAttribute>(assembly, attribute => attribute.Company) ?? "Code-iX";
        CopyrightText.Text = AttributeValue<AssemblyCopyrightAttribute>(assembly, attribute => attribute.Copyright) ?? "Code-iX";
    }

    private static string? AttributeValue<TAttribute>(Assembly assembly, Func<TAttribute, string?> selector)
        where TAttribute : Attribute =>
        assembly.GetCustomAttribute<TAttribute>() is { } attribute
            ? selector(attribute)
            : null;

    private static string DisplayVersion(Assembly assembly, AssemblyName name)
    {
        var informationalVersion = AttributeValue<AssemblyInformationalVersionAttribute>(
            assembly,
            attribute => attribute.InformationalVersion);

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataIndex = informationalVersion.IndexOf('+', StringComparison.Ordinal);
            return metadataIndex > 0
                ? informationalVersion[..metadataIndex]
                : informationalVersion;
        }

        return name.Version?.ToString(3) ?? "1.0.0";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void GitHubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true,
        });

        e.Handled = true;
    }
}
