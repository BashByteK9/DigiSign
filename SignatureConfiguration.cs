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

        public string Copy1Label { get; set; } = "Original for Buyer";
        public bool ExtraCopiesEnabled { get; set; }
        public bool PrintAllCopies { get; set; }
        public string Copy2Label { get; set; }
        public string Copy3Label { get; set; }
        public string Copy4Label { get; set; }

        public float CopyLabelX { get; set; }
        public float CopyLabelY { get; set; }
        public float CopyLabelWidth { get; set; }
        public float CopyLabelHeight { get; set; }

        public SignatureConfiguration(float x, float y, float width, float height)
        {
            XCoordinate = x;
            YCoordinate = y;
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Returns the ordered list of copy labels that should actually be signed:
        /// Copy 1 is always included (falling back to the default if blank); Copy 2-4
        /// are included only when Extra Copies is enabled and their label isn't blank.
        /// </summary>
        public List<string> GetCopyLabelsToSign()
        {
            var labels = new List<string>
            {
                string.IsNullOrWhiteSpace(Copy1Label) ? "Original for Buyer" : Copy1Label.Trim()
            };

            if (ExtraCopiesEnabled)
            {
                labels.AddRange(
                    new[] { Copy2Label, Copy3Label, Copy4Label }
                        .Where(label => !string.IsNullOrWhiteSpace(label))
                        .Select(label => label.Trim()));
            }

            return labels;
        }
    }
}
