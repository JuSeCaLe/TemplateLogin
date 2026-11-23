using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Login.Infrastructure.Model;

namespace Login.Infrastructure.Services;

public class MemorialGenerationService
{
    // Acepta dos formatos de placeholder en el .docx:
    //   - [NombreVariable]  (el que se documenta a los admins que suben plantillas)
    //   - {{NOMBRE_VARIABLE}} (formato legado de las plantillas creadas antes
    //     de este módulo — se sigue soportando para no romperlas)
    // Los nombres se comparan normalizados (sin tildes/espacios/guiones,
    // mayúsculas), así que [TipoProceso], [TIPO_PROCESO] y {{TIPO_PROCESO}}
    // resuelven al mismo valor.
    private static readonly Regex PlaceholderRegex = new(
        @"\{\{\s*(?<legacy>[\p{L}0-9_]+)\s*\}\}|\[\s*(?<bracket>[\p{L}0-9_ ]+?)\s*\]",
        RegexOptions.Compiled);

    public static string NormalizeKey(string name)
    {
        var noSpaces = name.Replace(" ", "").Replace("_", "");
        var formD = noSpaces.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant();
    }

    // Nombres de placeholder detectados en el documento, tal como aparecen
    // escritos (sin normalizar) — para mostrarlos en el admin al subir o
    // reemplazar una plantilla. No hace falta que el .docx tenga contenido
    // aún cargado en Content; recibe los bytes crudos directamente.
    public static List<string> ExtractPlaceholders(byte[] docxBytes)
    {
        using var ms = new MemoryStream(docxBytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var mainPart = doc.MainDocumentPart;
        if (mainPart?.Document.Body is null) return [];

        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Scan(DocumentFormat.OpenXml.OpenXmlElement root)
        {
            foreach (var p in root.Descendants<Paragraph>())
            {
                var full = string.Concat(p.Descendants<Text>().Select(t => t.Text));
                foreach (Match m in PlaceholderRegex.Matches(full))
                {
                    var name = (m.Groups["legacy"].Success ? m.Groups["legacy"].Value : m.Groups["bracket"].Value).Trim();
                    if (seen.Add(name)) found.Add(name);
                }
            }
        }

        Scan(mainPart.Document.Body);
        foreach (var h in mainPart.HeaderParts) Scan(h.Header);
        foreach (var f in mainPart.FooterParts) Scan(f.Footer);

        return found;
    }

    public byte[] Generate(MemorialTemplate template, IReadOnlyDictionary<string, string> values)
    {
        if (template.Content is null || template.Content.Length == 0)
            throw new InvalidOperationException($"La plantilla '{template.Name}' no tiene archivo cargado. Reemplázala desde el módulo de plantillas.");

        // Normaliza las llaves del diccionario una sola vez.
        var normalizedValues = values.ToDictionary(kv => NormalizeKey(kv.Key), kv => kv.Value ?? "");

        using var ms = new MemoryStream();
        ms.Write(template.Content, 0, template.Content.Length);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var mainPart = doc.MainDocumentPart!;

            ReplaceInTree(mainPart.Document.Body!, normalizedValues);
            mainPart.Document.Save();

            foreach (var header in mainPart.HeaderParts)
            {
                ReplaceInTree(header.Header, normalizedValues);
                header.Header.Save();
            }

            foreach (var footer in mainPart.FooterParts)
            {
                ReplaceInTree(footer.Footer, normalizedValues);
                footer.Footer.Save();
            }
        }

        return ms.ToArray();
    }

    // Reemplaza placeholder por placeholder, párrafo por párrafo. Un mismo
    // token puede quedar repartido entre varias runs (Word las separa solo
    // por corrección ortográfica, cambios de formato a mitad de palabra,
    // etc.) — por eso se arma el texto completo del párrafo para detectar
    // el token; el valor final se pone en el primer <w:t> que ocupaba y el
    // resto de las runs que ocupaba quedan vacías. Los recortes se calculan
    // siempre sobre el texto ORIGINAL de cada nodo (nunca sobre uno ya
    // editado), para que dos placeholders que compartan una misma run — ej.
    // "[Radicado] - [TipoProceso]" sin ningún salto de run entre ellos — se
    // resuelvan bien los dos en vez de que el segundo corte mal el string
    // que dejó el primero.
    private static void ReplaceInTree(DocumentFormat.OpenXml.OpenXmlElement root, Dictionary<string, string> normalizedValues)
    {
        foreach (var paragraph in root.Descendants<Paragraph>().ToList())
        {
            var textNodes = paragraph.Descendants<Text>().ToList();
            if (textNodes.Count == 0) continue;

            var original = textNodes.Select(t => t.Text).ToArray();
            var offsets = new int[original.Length];
            var cursor = 0;
            for (var i = 0; i < original.Length; i++)
            {
                offsets[i] = cursor;
                cursor += original[i].Length;
            }
            var full = string.Concat(original);

            // edits[nodeIndex] = lista de (localStart, localEnd, texto de reemplazo),
            // sin solapes entre sí (las coincidencias del regex tampoco se solapan).
            var edits = new Dictionary<int, List<(int Start, int End, string Value)>>();

            foreach (Match m in PlaceholderRegex.Matches(full))
            {
                var name = m.Groups["legacy"].Success ? m.Groups["legacy"].Value : m.Groups["bracket"].Value;
                var key = NormalizeKey(name);
                // Token desconocido: se deja tal cual escrito en vez de
                // desaparecer en silencio (ayuda a detectar un typo del
                // admin al subir la plantilla).
                if (!normalizedValues.TryGetValue(key, out var value)) continue;

                var start = m.Index;
                var end = m.Index + m.Length; // exclusivo

                var i0 = IndexOfOffset(offsets, start);
                var i1 = IndexOfOffset(offsets, end - 1);

                if (i0 == i1)
                {
                    AddEdit(edits, i0, start - offsets[i0], end - offsets[i0], value);
                }
                else
                {
                    AddEdit(edits, i0, start - offsets[i0], original[i0].Length, value);
                    for (var k = i0 + 1; k < i1; k++)
                        AddEdit(edits, k, 0, original[k].Length, "");
                    AddEdit(edits, i1, 0, end - offsets[i1], "");
                }
            }

            foreach (var (nodeIndex, nodeEdits) in edits)
            {
                var sb = new StringBuilder();
                var lastPos = 0;
                foreach (var edit in nodeEdits.OrderBy(e => e.Start))
                {
                    sb.Append(original[nodeIndex], lastPos, edit.Start - lastPos);
                    sb.Append(edit.Value);
                    lastPos = edit.End;
                }
                sb.Append(original[nodeIndex], lastPos, original[nodeIndex].Length - lastPos);

                textNodes[nodeIndex].Text = sb.ToString();
                textNodes[nodeIndex].Space = SpaceProcessingModeValues.Preserve;
            }
        }
    }

    private static void AddEdit(Dictionary<int, List<(int, int, string)>> edits, int nodeIndex, int start, int end, string value)
    {
        if (!edits.TryGetValue(nodeIndex, out var list))
        {
            list = [];
            edits[nodeIndex] = list;
        }
        list.Add((start, end, value));
    }

    private static int IndexOfOffset(int[] offsets, int charIndex)
    {
        for (var i = offsets.Length - 1; i >= 0; i--)
        {
            if (charIndex >= offsets[i]) return i;
        }
        return 0;
    }
}
