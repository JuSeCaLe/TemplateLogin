using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Login.Infrastructure.Model;

namespace Login.Infrastructure.Services;

// Valores para llenar los placeholders de una plantilla de memorial. Deben
// coincidir exactamente (incluidas mayúsculas) con los tokens insertados en
// los .docx de Templates/Memoriales — ver el conversor usado para crearlos.
public record MemorialFieldValues(
    string Juzgado,
    string Ciudad,
    string TipoProceso,
    string Demandante,
    string Demandado,
    string DemandadoCedula,
    string Radicado
);

public class MemorialGenerationService
{
    private readonly string _templatesDir;

    public MemorialGenerationService()
    {
        _templatesDir = Path.Combine(AppContext.BaseDirectory, "Templates", "Memoriales");
    }

    public byte[] Generate(MemorialTemplate template, MemorialFieldValues values)
    {
        var path = Path.Combine(_templatesDir, template.FileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"No se encontró el archivo de la plantilla '{template.FileName}'.", path);

        using var ms = new MemoryStream();
        using (var fs = File.OpenRead(path))
            fs.CopyTo(ms);
        ms.Position = 0;

        var replacements = new Dictionary<string, string>
        {
            ["{{JUZGADO}}"] = values.Juzgado,
            ["{{CIUDAD}}"] = values.Ciudad,
            ["{{TIPO_PROCESO}}"] = values.TipoProceso,
            ["{{DEMANDANTE}}"] = values.Demandante,
            ["{{DEMANDADO}}"] = values.Demandado,
            ["{{DEMANDADO_CEDULA}}"] = values.DemandadoCedula,
            ["{{RADICADO}}"] = values.Radicado,
        };

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var mainPart = doc.MainDocumentPart!;

            ReplaceTexts(mainPart.Document.Body!, replacements);
            mainPart.Document.Save();

            foreach (var header in mainPart.HeaderParts)
            {
                ReplaceTexts(header.Header, replacements);
                header.Header.Save();
            }

            foreach (var footer in mainPart.FooterParts)
            {
                ReplaceTexts(footer.Footer, replacements);
                footer.Footer.Save();
            }
        }

        return ms.ToArray();
    }

    // Cada placeholder vive en una sola run (así se insertaron al convertir
    // las plantillas), así que un reemplazo directo por Text alcanza — no
    // hace falta reconstruir párrafos/runs.
    private static void ReplaceTexts(OpenXmlElement root, Dictionary<string, string> replacements)
    {
        foreach (var text in root.Descendants<Text>())
        {
            var value = text.Text;
            var changed = false;
            foreach (var (token, replacement) in replacements)
            {
                if (!value.Contains(token)) continue;
                value = value.Replace(token, replacement ?? "");
                changed = true;
            }
            if (changed) text.Text = value;
        }
    }
}
