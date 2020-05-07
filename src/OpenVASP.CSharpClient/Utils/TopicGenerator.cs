using System;
using Nethereum.Hex.HexConvertors.Extensions;

namespace OpenVASP.CSharpClient.Utils
{
    public static class TopicGenerator
    {
        private static readonly Random _random = new Random();

        public static string GenerateSessionTopic()
        {
            var bytes = new byte[4];
            _random.NextBytes(bytes);

            return bytes.ToHex(true);
        }
    }
}