using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Iftm.ComputedProperties {
    public class AllPropertiesChanged {
        private static PropertyChangedEventArgs? _all;

        public static PropertyChangedEventArgs EventArgs {
            get {
                if (_all == null) _all = new PropertyChangedEventArgs(null);
                return _all;
            }
        }
}
}
