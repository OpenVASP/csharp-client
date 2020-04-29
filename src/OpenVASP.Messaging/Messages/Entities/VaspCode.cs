using System;
using System.Collections.Generic;
using System.Text;

namespace OpenVASP.Messaging.Messages.Entities
{
    public class VaspCode
    {
        private VaspCode(string vaspCodeHex)
        {
            this.Code = vaspCodeHex;
        }
        public string Code { get; }

        public static VaspCode Create(string vaspCodeHex)
        {
            var result = new VaspCode(vaspCodeHex);

            return result;
        }
    }
}
