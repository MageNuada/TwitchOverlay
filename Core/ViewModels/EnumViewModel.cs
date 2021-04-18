using ReactiveUI;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace ChatOverlay.Core
{
    public class EnumViewModel : ReactiveObject, IEnumerable
    {
        private Type? _enumType;
        private IEnumerable? _values;

        public IEnumerable? Values
        {
            get => _values;
            protected set
            {
                _values = value;
                this.RaisePropertyChanged();
            }
        }

        public EnumViewModel()
        {
            _values = Enumerable.Empty<string>();
        }

        public EnumViewModel(Type? type)
        {
            EnumType = type;
        }


        public Type? EnumType
        {
            get => _enumType;
            set
            {
                _enumType = value;
                this.RaisePropertyChanged();
                InitValues();
            }
        }

        private void InitValues()
        {
            Values = EnumType?.GetFields(BindingFlags.Public | BindingFlags.Static).Select(x => x.GetValue(EnumType));
        }


        public IEnumerator GetEnumerator()
        {
            return Values?.GetEnumerator();
        }
    }
}
