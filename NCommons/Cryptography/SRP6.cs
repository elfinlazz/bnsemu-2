using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace NCommons.Cryptography
{
    /// <summary>
    /// This is a specific SRP-6 implementation for use with the Sts server.
    /// </summary>
    public sealed class SRP6
    {
        /*
         * Instances of this type carry a lot of information and are unique to each client 
         *  and should be discarded as soon as a user is done with it (client is successfully authentificated).
         * 
         * There is no Thread-safety added to this because the user should not be accessing from multiple threads.
         *  I make this assumption because of the BasicServer implementation.
         * 
         * TODO: 
         *  - Various optimizations.
         *   i.e. some hash are made into BigInteger but only used as byte array (useless computations made).
         *        some functions could be optimized (like HashKey())
         *        ReceiveClientProof currently does some reversing on some bytes but I think the algorithm could be made more efficient.
         */

        // used for hashing of BigIntegers. Removes some small empty array allocation overhead.
        private static readonly Byte[][] s_bytesPadding = new Byte[4][] { 
            new Byte[0], new Byte[1], new Byte[2], new Byte[3] };
        private static readonly BigInteger g = 2;
        private static readonly BigInteger N = new BigInteger(new Byte[] {
            0xE3, 0x06, 0xEB, 0xC0, 0x2F, 0x1D, 0xC6, 0x9F, 0x5B, 0x43, 0x76, 0x83, 0xFE, 0x38, 0x51, 0xFD,
            0x9A, 0xAA, 0x6E, 0x97, 0xF4, 0xCB, 0xD4, 0x2F, 0xC0, 0x6C, 0x72, 0x05, 0x3C, 0xBC, 0xED, 0x68,
            0xEC, 0x57, 0x0E, 0x66, 0x66, 0xF5, 0x29, 0xC5, 0x85, 0x18, 0xCF, 0x7B, 0x29, 0x9B, 0x55, 0x82,
            0x49, 0x5D, 0xB1, 0x69, 0xAD, 0xF4, 0x8E, 0xCE, 0xB6, 0xD6, 0x54, 0x61, 0xB4, 0xD7, 0xC7, 0x5D,
            0xD1, 0xDA, 0x89, 0x60, 0x1D, 0x5C, 0x49, 0x8E, 0xE4, 0x8B, 0xB9, 0x50, 0xE2, 0xD8, 0xD5, 0xE0,
            0xE0, 0xC6, 0x92, 0xD6, 0x13, 0x48, 0x3B, 0x38, 0xD3, 0x81, 0xEA, 0x96, 0x74, 0xDF, 0x74, 0xD6,
            0x76, 0x65, 0x25, 0x9C, 0x4C, 0x31, 0xA2, 0x9E, 0x0B, 0x3C, 0xFF, 0x75, 0x87, 0x61, 0x72, 0x60,
            0xE8, 0xC5, 0x8F, 0xFA, 0x0A, 0xF8, 0x33, 0x9C, 0xD6, 0x8D, 0xB3, 0xAD, 0xB9, 0x0A, 0xAF, 0xEE, 0x00 });

        private Object srpLock = new Object();
        private Byte state;
        private SHA256 sha256;
        private RandomNumberGenerator rng;

        // Note: These are only variables that need to be reused.
        private BigInteger s;
        private BigInteger v;
        private BigInteger b;
        private BigInteger B;
        private BigInteger S; // session key
        private BigInteger K; // hashed session key

        private Byte[] I;
        private BigInteger p;

        static SRP6()
        {
            //s_sha256 = SHA256.Create();
            //s_rng = RandomNumberGenerator.Create();
            //k = Hash(N, g);
        }

        public SRP6()
        {
            // init anything that changes for every Srp
            sha256 = SHA256.Create();
            rng = RandomNumberGenerator.Create();
            s = MakeRandomBigInteger(8);
            state = 0;
        }

        public BigInteger SessionKey
        {
            get
            {
                if (state != 2)
                    throw new SRP6InvalidStateException();
                else
                    return S;
            }
        }

        public void ReceiveLoginStartInfo(String username, String password, BinaryWriter keyDataWriter)
        {
            if (state != 0)
                throw new SRP6InvalidStateException();

            I = Encoding.ASCII.GetBytes(username);

            sha256.Initialize();
            // this is temporary, eventually that would be saved/used differently.
            p = SRPHelpers.GetBigIntegerFromBytes(sha256.ComputeHash(Encoding.ASCII.GetBytes(username + ":" + password)));

            BigInteger x = Hash(s, p);

            v = BigInteger.ModPow(g, x, N);
            b = MakeRandomBigInteger(128);
            B = (((Hash(N, g) * v) % N) + (BigInteger.ModPow(g, b, N))) % N;
            // validated up to here.

            // TODO: Make the right checks for this.
            Byte[] s_bytes = SRPHelpers.GetBytesFromBigInteger(s);
            Byte[] B_bytes = SRPHelpers.GetBytesFromBigInteger(B);

            keyDataWriter.Write(s_bytes.Length); // 8
            keyDataWriter.Write(s_bytes, 0, s_bytes.Length);
            keyDataWriter.Write(B_bytes.Length); // 128
            keyDataWriter.Write(B_bytes, 0, B_bytes.Length);

            state = 1;
        }

        public void ReceiveClientProof(BinaryReader clientKeyDataReader, BinaryWriter serverKeyDataWriter, out byte[] key)
        {
            if (state != 1)
                throw new SRP6InvalidStateException();

            // receive M_c (proof) & A (public ephemerial key)
            Int32 len = clientKeyDataReader.ReadInt32();
            BigInteger A = SRPHelpers.GetBigIntegerFromBytes(clientKeyDataReader.ReadBytes(len));
            len = clientKeyDataReader.ReadInt32();
            Byte[] M_clientBytes = clientKeyDataReader.ReadBytes(len);

            if (A == 0) // A % N ??
                throw new SRP6SafeguardException("SRP6 received invalid 'A' from server (A == 0)");

            // Reverse bytes received from server.
            //SRPHelpers.ReverseBytesAsUInt32(M_clientBytes);
            BigInteger M_client = SRPHelpers.GetBigIntegerFromBytes(M_clientBytes);

            // calculate random scrambling parameter
            BigInteger u = Hash(A, B);
            // compute session key (not sure about this one, check if it's the right alg)
            S = BigInteger.ModPow((A * BigInteger.ModPow(v, u, N)) % N, b, N);
            K = HashKey(S); // I think the client uses this key to encrypt. It doesnt seem to use S at all

            // return the key
            key = SRPHelpers.GetBytesFromBigInteger(K);

            sha256.Initialize();
            Byte[] IHash = sha256.ComputeHash(I);
            BigInteger hash = Hash(Hash(N) ^ Hash(g),
                SRPHelpers.GetBigIntegerFromBytes(IHash), s, A, B, K);

            // Reverse the array.
            Byte[] bytes = SRPHelpers.GetBytesFromBigInteger(hash);
            SRPHelpers.ReverseBytesAsUInt32(bytes);
            hash = SRPHelpers.GetBigIntegerFromBytes(bytes);

            if (M_client != hash)
                throw new SRP6SafeguardException("SRP6 could not validate proof 'K'.");

            BigInteger M = Hash(A, M_client, K);
            Byte[] M_bytes = SRPHelpers.GetBytesFromBigInteger(M);

            //reverse byte
            SRPHelpers.ReverseBytesAsUInt32(M_bytes);

            serverKeyDataWriter.Write(M_bytes.Length); // 32
            serverKeyDataWriter.Write(M_bytes, 0, M_bytes.Length);
        }


        private BigInteger MakeRandomBigInteger(Int32 bytes)
        {
            Byte[] r = new Byte[bytes];
            rng.GetBytes(r);
            return SRPHelpers.GetBigIntegerFromBytes(r) % N;
        }

        public BigInteger Hash(BigInteger integer)
        {
            sha256.Initialize();

            Byte[] bytes = SRPHelpers.GetBytesFromBigInteger(integer);

            int padding = (4 - bytes.Length % 4) % 4;

            // if there's no need for padding just return a simple hash.
            if (padding == 0)
                return SRPHelpers.GetBigIntegerFromBytes(sha256.ComputeHash(bytes));

            sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
            sha256.TransformFinalBlock(s_bytesPadding[padding], 0, padding);

            // this doesnt seem to be inverted.
            return SRPHelpers.GetBigIntegerFromBytes(sha256.Hash);
        }

        private BigInteger Hash(params BigInteger[] integers)
        {
            if (integers == null || integers.Length <= 0)
                throw new ArgumentException();

            sha256.Initialize();
            Byte[] bytes;

            // iterates all but the last
            int lastIndex = integers.Length - 1;
            for (int i = 0; i < integers.Length; i++)
            {
                // padding.
                bytes = SRPHelpers.GetBytesFromBigInteger(integers[i]);
                int padding = bytes.Length % 4;

                sha256.TransformBlock(bytes, 0, bytes.Length, null, 0);
                if (padding != 0)
                    sha256.TransformBlock(s_bytesPadding[4 - padding], 0, 4 - padding, null, 0);
            }

            // finalize with empty block (no modification)
            sha256.TransformFinalBlock(s_bytesPadding[0], 0, 0);

            // we need to reverse the hash result as integers. I'm unsure why, I guess the game does it.
            Byte[] hash = sha256.Hash;
            SRPHelpers.ReverseBytesAsUInt32(hash);

            return SRPHelpers.GetBigIntegerFromBytes(hash);
        }

        // TODO: Optimize this method
        private BigInteger HashKey(BigInteger key)
        {
            sha256.Initialize();
            Byte[] output = new Byte[64];
            Byte[] keyBytes = SRPHelpers.GetBytesFromBigInteger(key);
            Byte[] v13;
            Byte[] hash = new Byte[32];

            Int32 keyLen = keyBytes.Length;
            Int32 len0 = keyLen;
            Int32 len;

            Int32 ptr0 = 0;

            if (keyLen > 4)
            {
                do
                {
                    if (keyBytes[ptr0] == 0)
                        break;

                    len0--;
                    ptr0++;
                }
                while (len0 > 4);
            }

            len = len0 >> 1;
            Int32 v6 = 0;

            v13 = new Byte[len];

            if (len >> 1 != 0)
            {
                int v7 = len0 + ptr0 - 1;
                do
                {
                    v13[v6++] = keyBytes[v7];
                    v7 -= 2;
                }
                while (v6 < len);
            }

            hash = sha256.ComputeHash(v13);

            // copy hash to output
            for (int i = 0; i < 32; i++)
                output[i * 2] = hash[i];


            // make second part
            Int32 v9 = 0;


            if (len != 0)
            {
                Int32 v10 = len0 + ptr0 - 2;
                do
                {
                    v13[v9++] = keyBytes[v10];
                    v10 -= 2;
                }
                while (v9 < len);
            }

            hash = sha256.ComputeHash(v13);

            // copy hash to output
            for (int i = 0; i < 32; i++)
                output[i * 2 + 1] = hash[i];

            return SRPHelpers.GetBigIntegerFromBytes(output);
        }

        static class SRPHelpers
        {
            /*
             * These helpers are required for the following reasons:
             *  - Byte arrays passed to and received from BigInteger class are signed but the game
             *     consider them all as unsigned, so we need to "fake" the input/output of BigInteger.
             *  - When generating hash, the game's class uses the internal UInt32 array.
             *     That means that the game could have hash one to three more zero'd bytes than our class.
             * 
             * The solution here is to:
             *  - Make sure arrays used to create BigInt are not going to be considered negative (if last >0x7F
             */

            // should this contain padding bytes? I don't think so.. Let hash take care of that.
            public static Byte[] GetBytesFromBigInteger(BigInteger integer)
            {
                Byte[] output = integer.ToByteArray();

                // I don't think we care for anything else than 0-terminated here.
                // it would simply mean that the biginteger is negative 
                // if anything we should throw and exception because it's an error for us.
                if (output[output.Length - 1] != 0)
                    return output;

                // if it's zero-terminated then make a new copy without it.
                Byte[] output2 = new Byte[output.Length - 1];
                Buffer.BlockCopy(output, 0, output2, 0, output2.Length);

                return output2;
            }

            public static BigInteger GetBigIntegerFromBytes(Byte[] bytes)
            {
                if (bytes[bytes.Length - 1] <= 0x7F)
                    return new BigInteger(bytes);

                // if last byte is > 0x7F we need to use a new zero-terminated array.
                Byte[] bytes2 = new Byte[bytes.Length + 1];
                Buffer.BlockCopy(bytes, 0, bytes2, 0, bytes.Length);

                return new BigInteger(bytes2);
            }

            public static void ReverseBytesAsUInt32(Byte[] array)
            {
                // works, but thats sloppy.
                int j = array.Length - 4;
                for (int i = 0; i < array.Length / 2; i += 4, j -= 4)
                {
                    Byte b = array[i + 0];
                    array[i + 0] = array[j + 0];
                    array[j + 0] = b;
                    b = array[i + 1];
                    array[i + 1] = array[j + 1];
                    array[j + 1] = b;
                    b = array[i + 2];
                    array[i + 2] = array[j + 2];
                    array[j + 2] = b;
                    b = array[i + 3];
                    array[i + 3] = array[j + 3];
                    array[j + 3] = b;
                }
            }

        }
    }


    public class SRP6InvalidStateException : InvalidOperationException
    {
        public SRP6InvalidStateException() :
            base("The SRP6 instance was in an invalid state for the operation.") { }
        public SRP6InvalidStateException(string message) :
            base(message) { }
        public SRP6InvalidStateException(string message, Exception innerException) :
            base(message, innerException) { }
    }

    public class SRP6SafeguardException : ApplicationException
    {
        public SRP6SafeguardException() :
            base("One of the SRP6 safeguard conditions were not met.") { }
        public SRP6SafeguardException(string message) :
            base(message) { }
        public SRP6SafeguardException(string message, Exception innerException) :
            base(message, innerException) { }
    }
}
