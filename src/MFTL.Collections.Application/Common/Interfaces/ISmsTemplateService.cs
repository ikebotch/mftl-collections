namespace MFTL.Collections.Application.Common.Interfaces;

public interface ISmsTemplateService
{
    string Render(string template, object data);
}
