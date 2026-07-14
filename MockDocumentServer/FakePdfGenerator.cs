using System;
using System.IO;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace MockDocumentServer
{
    internal static class FakePdfGenerator
    {
        public static byte[] Generate(string token, string documentType, string fileName)
        {
            using (var ms = new MemoryStream())
            {
                var doc = new Document(PageSize.A4);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();
                doc.Add(new Paragraph($"MOCK {documentType.ToUpperInvariant()} DOCUMENT"));
                doc.Add(new Paragraph($"Token: {token}"));
                doc.Add(new Paragraph($"File: {fileName}"));
                doc.Add(new Paragraph($"Generated: {DateTime.UtcNow:O}"));
                doc.Close();
                return ms.ToArray();
            }
        }
    }
}
