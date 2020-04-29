using System.Text;
using Nethereum.Hex.HexConvertors.Extensions;

namespace OpenVASP.Messaging.Messages.Entities
{
    /// <summary>
    /// VAAN
    /// </summary>
    public class VirtualAssetsAccountNumber
    {
        public VaspCode VaspCode { get; }

        public string CustomerNumber { get; }

        public string Vaan { get; }

        private VirtualAssetsAccountNumber(VaspCode vaspCode, string customerSpecificNumberHex)
        {
            VaspCode = vaspCode;
            CustomerNumber = customerSpecificNumberHex;

            var vaan = vaspCode.Code + customerSpecificNumberHex;
            var checksum = GetChecksum8Modulo256(vaan);
            var checkSumStr = (new byte[] { checksum }).ToHex(false);

            Vaan = $"{vaan}{checkSumStr}";
        }

        public static VirtualAssetsAccountNumber Create(string vaspCode, string customerSpecificNumberHex)
        {
            var vasp = VaspCode.Create(vaspCode);
            var result = new VirtualAssetsAccountNumber(vasp, customerSpecificNumberHex);

            return result;
        }

        private static byte GetChecksum8Modulo256(string vaan)
        {
            byte[] data = vaan.HexToByteArray();
            byte check = 0;

            for (int i = 0; i < data.Length; i++)
            {
                check += data[i];
            }

            return check;
        }
    }
}
