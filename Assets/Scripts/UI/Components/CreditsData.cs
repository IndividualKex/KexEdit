using System;
using System.Collections.Generic;

using KexEdit.Legacy;
namespace KexEdit.UI {
    [Serializable]
    public class CreditsData {
        public List<CreditCategory> Credits = new();
    }

    [Serializable]
    public class CreditCategory {
        public string Category;
        public List<string> Names = new();
    }
}