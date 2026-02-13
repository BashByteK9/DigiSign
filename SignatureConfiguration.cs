using System.Collections.Generic;
using System.Linq;

namespace DigiSign
{
    /// <summary>
    /// Configuration for digital signature appearance and placement
    /// </summary>
    public class SignatureConfiguration
    {
        public float XCoordinate { get; set; }
        public float YCoordinate { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string SignOnPage { get; set; } // F=First, E=Each, L=Last

        public SignatureConfiguration(float x, float y, float width, float height, string signOnPage)
        {
            XCoordinate = x;
            YCoordinate = y;
            Width = width;
            Height = height;
            SignOnPage = signOnPage ?? "L"; // Default to Last page
        }

        /// <summary>
        /// Determines which pages should be signed based on SignOnPage setting
        /// </summary>
        /// <param name="totalPages">Total number of pages in the PDF</param>
        /// <returns>List of page numbers to sign (1-based index)</returns>
        public List<int> GetPagesToSign(int totalPages)
        {
            var pagesToSign = new List<int>();

            switch (SignOnPage?.ToUpper())
            {
                case "F":
                    pagesToSign.Add(1); // First page
                    break;
                case "E":
                    pagesToSign.AddRange(Enumerable.Range(1, totalPages)); // Each page
                    break;
                case "L":
                default:
                    pagesToSign.Add(totalPages); // Last page
                    break;
            }

            return pagesToSign;
        }
    }
}
