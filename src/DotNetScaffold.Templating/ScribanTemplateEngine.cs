using System.Reflection;
using Scriban;
using Scriban.Runtime;

namespace DotNetScaffold.Templating;

public sealed class ScribanTemplateEngine : ITemplateEngine
{
    private static readonly Assembly ResourceAssembly = typeof(ScribanTemplateEngine).Assembly;
    private const string ResourcePrefix = "DotNetScaffold.Templating.Templates.";

    public string Render(string templateName, object model)
    {
        var templateText = LoadTemplateText(templateName);

        var template = Template.Parse(templateText, templateName);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"Template '{templateName}' failed to parse: {string.Join("; ", template.Messages)}");
        }

        var scriptObject = new ScriptObject();
        scriptObject.Import(model, renamer: member => member.Name);

        var context = new TemplateContext { MemberRenamer = member => member.Name };
        context.PushGlobal(scriptObject);

        return template.Render(context);
    }

    private static string LoadTemplateText(string templateName)
    {
        var resourceName = ResourcePrefix + templateName.Replace('/', '.').Replace('\\', '.');
        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Template '{templateName}' not found (expected embedded resource '{resourceName}').");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
