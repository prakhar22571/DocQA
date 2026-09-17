using UglyToad.PdfPig;

namespace DocQA;

public static class PdfTextExtractor
{
    public static string ExtractText(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);
        return string.Join("\n\n", document.GetPages().Select(page => page.Text));
    }
}
