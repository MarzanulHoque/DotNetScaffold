namespace DotNetScaffold.Templating;

/// <summary>
/// Renders externalized Scriban templates (SRS maintainability requirement: no
/// string-concatenation code generation). Template names are slash-separated paths relative to the
/// <c>Templates/</c> folder, e.g. <c>"Layered/AppDbContext.sbn"</c>.
/// </summary>
public interface ITemplateEngine
{
    string Render(string templateName, object model);
}
