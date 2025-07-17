using System;
using System.Collections.Generic;

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