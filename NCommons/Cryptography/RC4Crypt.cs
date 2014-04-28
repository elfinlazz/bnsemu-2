using System;

namespace NCommons.Cryptography
{
    public class RC4Crypt
    {
        private Byte[] s;
        private UInt32 j;
        private UInt32 i;

        public RC4Crypt(byte[] key)
        {
            s = new Byte[256];
            j = 0;
            i = 0;

            for (int index = 0; index < 256; index++)
                s[index] = (Byte)index;

            Int32 index1 = 0;
            Int32 s_index = 0;
            Int32 v12 = 256;
            Byte v4 = 0;

            do
            {
                if (index1 >= key.Length)
                    throw new ApplicationException("index1 < bytes");

                Byte v8 = s[s_index];
                v4 = (Byte)((v4 + key[index1++] + s[s_index]) & 0xFF);
                s[s_index] = s[v4];
                s[v4] = v8;

                if (index1 == key.Length)
                    index1 = 0;
                s_index++;
            } while (v12-- != 1);
        }

        public void EncryptBuffer(Byte[] buffer, long offset, long count)
        {
            for (int index = 0; index < count; index++)
            {
                long bufferIndex = index + offset;

                i = (i + 1) & 0xFF;
                j = (j + s[i]) & 0xFF;
                byte swap = s[i];
                s[i] = s[j];
                s[j] = swap;

                buffer[bufferIndex] = (byte)((buffer[bufferIndex]) ^ s[(s[j] + s[i]) & 0xFF]);
            }
        }

        public Byte[] Encrypt(Byte[] input)
        {
            Byte[] output = new Byte[input.Length];

            int index = 0;

            do
            {
                i = (i + 1) & 0xFF;
                j = (j + s[i]) & 0xFF;
                byte swap = s[i];
                s[i] = s[j];
                s[j] = swap;

                output[index] = (byte)((input[index]) ^ s[(s[j] + s[i]) & 0xFF]);
                // i put index++ here instead and we dont have to do -1
                ++index;
            }
            while (index != input.Length); // + 1 or - 1?

            return output;
        }
    }
}
