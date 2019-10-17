using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;

namespace Iftm.ComputedProperties {

    public class WithCachedProperties : WithComputedProperties, IIsPropertyValid {
        private InPlaceList<string> _validProperties;

        protected override void OnPropertyChanged(string? name) {
            if (name == null) {
                _validProperties.Clear();
            }
            else {
                var idx = _validProperties.IndexOf(name);
                if (idx >= 0) {
                    var lastPos = _validProperties.Count - 1;
                    _validProperties[idx] = _validProperties[lastPos];
                    _validProperties.RemoveAt(lastPos);
                }
            }

            base.OnPropertyChanged(name);
        }

        bool IIsPropertyValid.IsPropertyValid(string name) => _validProperties.Contains(name);
        void IIsPropertyValid.SetPropertyValid(string name) {
            if (!_validProperties.Contains(name)) _validProperties.Add(name);
        }
    }

}
