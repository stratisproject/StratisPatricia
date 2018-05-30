using System;
using System.Collections.Generic;

namespace Stratis.Patricia
{
    /// <summary>
    /// Used to access data in the trie. When retrieving an item from the trie, we need to break the key down into 4-bit parts.
    /// Each branch node can hold up to 16 links to other nodes, so each of these represents a half-byte (nibble).
    ///
    /// This class is a kind of byte reader that shifts 4 bits every time as it moves down the trie. 
    /// </summary>
    internal sealed class Key
    {
        public const int OddOffsetFlag = 0x1;
        public const int TerminatorFlag = 0x2;
        private readonly byte[] keyBytes;
        private readonly int off;

        public int Length => (this.keyBytes.Length << 1) - this.off;

        public bool IsEmpty => this.Length == 0;

        public bool IsTerminal
        {
            get
            {
                if (IsEmpty)
                    return true;

                return ((keyBytes[0] >> 4) & TerminatorFlag) != 0;
            }
        }

        public static Key FromNormal(byte[] key)
        {
            return new Key(key);
        }

        public static Key FromPacked(byte[] key)
        {
            return new Key(key, ((key[0] >> 4) & OddOffsetFlag) != 0 ? 1 : 2);
        }

        public static Key Empty()
        {
            return new Key(new byte[0]);
        }

        public static Key SingleHex(int hex)
        {
            Key ret = new Key(new byte[1], 1);
            ret.SetHex(0, hex);
            return ret;
        }

        public Key(byte[] key, int off = 0)
        {
            this.off = off;
            this.keyBytes = key;
        }

        public byte[] ToPacked()
        {
            int flags = ((this.off & 1) != 0 ? OddOffsetFlag : 0) | (this.IsTerminal ? TerminatorFlag : 0);
            byte[] ret = new byte[this.Length / 2 + 1];
            int toCopy = (flags & OddOffsetFlag) != 0 ? ret.Length : ret.Length - 1;
            Array.Copy(this.keyBytes, this.keyBytes.Length - toCopy, ret, ret.Length - toCopy, toCopy);
            ret[0] &= 0x0F;
            ret[0] |= (byte) (flags << 4);
            return ret;
        }

        public int GetHex(int idx)
        {
            byte b = this.keyBytes[(this.off + idx) >> 1];
            return (((this.off + idx) & 1) == 0 ? (b >> 4) : b) & 0xF;
        }

        public Key Shift(int hexCnt)
        {
            return new Key(this.keyBytes, this.off + hexCnt);
        }

        private void SetHex(int idx, int hex)
        {
            int byteIdx = (this.off + idx) >> 1;
            if (((this.off + idx) & 1) == 0)
            {
                this.keyBytes[byteIdx] &= 0x0F;
                this.keyBytes[byteIdx] |= (byte) (hex << 4);
            }
            else
            {
                this.keyBytes[byteIdx] &= 0xF0;
                this.keyBytes[byteIdx] |= (byte) hex;
            }
        }

        public Key MatchAndShift(Key key)
        {
            int len = this.Length;
            int keyLength = key.Length;

            if (len < keyLength)
                return null;

            if ((this.off & 1) == (key.off & 1))
            {
                // optimization to compare whole keys bytes
                if ((this.off & 1) == 1)
                {
                    if (this.GetHex(0) != key.GetHex(0))
                        return null;
                }
                int idx1 = (this.off + 1) >> 1;
                int idx2 = (key.off + 1) >> 1;
                int l = keyLength >> 1;
                for (int i = 0; i < l; i++, idx1++, idx2++)
                {
                    if (this.keyBytes[idx1] != key.keyBytes[idx2])
                        return null;
                }
            }
            else
            {
                for (int i = 0; i < keyLength; i++)
                {
                    if (this.GetHex(i) != key.GetHex(i)) return null;
                }
            }
            return this.Shift(keyLength);
        }

        public Key Concat(Key key)
        {
            if (this.IsTerminal)
                throw new PatriciaTreeResolutionException("Can't append to terminal key: " + this + " + " + key);

            int length = this.Length;
            int keyLength = key.Length;
            int newLength = length + keyLength;
            byte[] newKeyBytes = new byte[(newLength + 1) >> 1];
            Key ret = new Key(newKeyBytes, newLength & 1);
            for (int i = 0; i < length; i++)
            {
                ret.SetHex(i, this.GetHex(i));
            }
            for (int i = 0; i < keyLength; i++)
            {
                ret.SetHex(length + i, key.GetHex(i));
            }
            return ret;
        }

        public Key GetCommonPrefix(Key key)
        {
            int prefixLen = 0;
            int keyLength = key.Length;


            while (prefixLen < this.Length && prefixLen < keyLength && this.GetHex(prefixLen) == key.GetHex(prefixLen))
            {
                prefixLen++;
            }

            byte[] prefixKey = new byte[(prefixLen + 1) >> 1];
            Key ret = new Key(prefixKey, (prefixLen & 1) == 0 ? 0 : 1);

            for (int i = 0; i < prefixLen; i++)
            {
                ret.SetHex(i, key.GetHex(i));
            }
            return ret;
        }

        public override bool Equals(object obj)
        {
            Key key = (Key)obj;

            if (key == null)
                return false;

            if (this.Length != key.Length)
                return false;

            for (int i = 0; i < this.Length; i++)
            {
                if (this.GetHex(i) != key.GetHex(i))
                    return false;
            }
            return this.IsTerminal == key.IsTerminal;
        }

        public override int GetHashCode()
        {
            var hashCode = -1563939470;
            hashCode = hashCode * -1521134295 + EqualityComparer<byte[]>.Default.GetHashCode(keyBytes);
            hashCode = hashCode * -1521134295 + off.GetHashCode();
            hashCode = hashCode * -1521134295 + IsTerminal.GetHashCode();
            hashCode = hashCode * -1521134295 + Length.GetHashCode();
            hashCode = hashCode * -1521134295 + IsEmpty.GetHashCode();
            return hashCode;
        }
    }
}
