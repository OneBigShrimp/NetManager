using System;
using System.Collections.Generic;
using System.Text;

namespace MyNetManager
{
    internal class MyStream
    {
        internal int Position { get; set; }
        /// <summary>
        /// 流是否可以用于解析(至少有一个类型(4字节)和长度标志位(4字节)才可以解析)
        /// </summary>
        internal bool CanAnalyse
        {
            get
            {
                return this.RemainCount >= 8;
            }
        }
        /// <summary>
        /// 流内剩余没有被解析的数据长度
        /// </summary>
        internal int RemainCount
        {
            get
            {
                return this.Oct.Count - this.Position;
            }
        }

        internal MyOctets Oct { get; set; }

        internal MyStream()
        {
            this.Oct = new MyOctets();
        }

        internal MyStream(int count)
        {
            this.Oct = new MyOctets(count);
        }

        internal MyStream(MyOctets oct)
        {
            this.Oct = oct;
        }


        public byte[] ToArray()
        {
            List<byte> result = new List<byte>();
            for (int i = Position; i < this.Oct.Count; i++)
            {
                result.Add(this.Oct.Datas[i]);
            }
            return result.ToArray();
        }

        internal byte ReadByte()
        {
            if (this.Oct.Count - this.Position < 1)
            {
                throw new LengthException();
            }
            return this.Oct.Datas[this.Position++];
        }

        internal int ReadInt()
        {
            return ((ReadByte() & 0xff) << 24) | ((ReadByte() & 0xff) << 16) | ((ReadByte() & 0xff) << 8) | (ReadByte() & 0xff);
        }

        internal float ReadFloat()
        {
            if (this.Oct.Count - this.Position < 4)
            {
                throw new LengthException();
            }
            float result = BitConverter.ToSingle(this.Oct.Datas, this.Position);
            this.Position += 4;
            return result;
        }

        internal string ReadString()
        {
            byte length = this.ReadByte();
            if (this.Oct.Count - this.Position < length)
            {
                throw new LengthException();
            }
            string result = System.Text.Encoding.UTF8.GetString(this.Oct.Datas, this.Position, length);
            this.Position += length;
            return result;
        }


        internal void WriteProtocolLength(int lengthPos, int length)
        {
            this.Oct.WriteProtocolLength(lengthPos, length);
        }

        /// <summary>
        /// 根据指定的增加长度重新计算Position
        /// </summary>
        /// <param name="addLength"></param>
        internal void ReCalculatePosition(int addLength)
        {
            int targetPos = this.Position + addLength;
            if (this.Oct.ReCalculateCount(targetPos))
            {
                this.Position = 0;
            }
            else
            {
                this.Position = targetPos;
            }
        }

        internal void Write(byte value)
        {
            this.Oct.Write(value);
        }

        internal void Write(int value)
        {
            this.Oct.Write(value);
        }

        internal void Write(float value)
        {
            this.Oct.Write(value);
        }

        internal void Write(string value)
        {
            this.Oct.Write(value);
        }



        internal void Clear()
        {
            this.Position = 0;
            this.Oct.Clear();
        }

    }




    internal class MyOctets
    {
        internal byte[] Datas { private set; get; }

        private const int defaultSize = 2;
        /// <summary>
        /// Datas超过这个长度,则下次EraseAndCompact时会重新计算尺寸
        /// </summary>
        private const int maxSize = 1024 * 64;

        internal int Count { private set; get; }

        internal MyOctets()
        {
            Datas = new byte[defaultSize];
        }

        internal MyOctets(int cap)
        {
            Datas = new byte[Roundup(cap)];
        }

        internal MyOctets(byte[] datas)
        {
            int length = Roundup(datas.Length);
            this.Datas = new byte[length];
            Buffer.BlockCopy(datas, 0, this.Datas, 0, datas.Length);
        }


        /// <summary>
        /// 专门为写入协议长度提供的方法,因为协议长度不能提前知道
        /// 要写入完毕才能知道,所以提供此方法
        /// </summary>
        internal void WriteProtocolLength(int lengthPos, int length)
        {
            int oldCount = this.Count;
            //修改写入位置
            this.Count = lengthPos;
            Write(length);
            this.Count = oldCount;
        }


        internal void Write(byte value)
        {
            CheckReSize();
            this.Datas[this.Count++] = value;
        }

        internal void Write(int value)
        {
            Write((byte)(value >> 24));
            Write((byte)(value >> 16));
            Write((byte)(value >> 8));
            Write((byte)(value));
        }

        internal void Write(float value)
        {
            byte[] fBytes = BitConverter.GetBytes(value);
            Write(fBytes);
        }

        internal void Write(string value)
        {
            byte[] stringBytes = Encoding.UTF8.GetBytes(value);
            if (stringBytes.Length > byte.MaxValue)
            {
                throw new Exception("字符串转Byte数组后长度超过256!!");
            }
            Write((byte)stringBytes.Length);
            Write(stringBytes);
        }


        internal void Write(MyOctets oct)
        {
            Write(oct.Datas, 0, oct.Count);
        }

        internal void Write(byte[] array, int startIndex = 0, int count = -1)
        {
            if (count < 0)
            {
                count = array.Length - startIndex;
            }

            CheckReSize(count);

            Buffer.BlockCopy(array, startIndex, this.Datas, this.Count, count);
            this.Count += count;
        }


        /// <summary>
        /// 根据Stream的Position重新计算Count
        /// </summary>
        /// <returns>是否发生重新计算</returns>
        internal bool ReCalculateCount(int position)
        {
            if (position < 0 || position > Count)
                throw new ArgumentOutOfRangeException();

            if (Datas.Length > maxSize)
            {//Datas堆积长度超过最大尺寸,则重新计算
                Count -= position;
                int upSize = Roundup(Count);
                var tmp = new byte[upSize];
                Buffer.BlockCopy(this.Datas, position, tmp, 0, Count);
                Datas = tmp;
                return true;
            }

            if (position == Count)
            {//数据刚好解析完毕,可以清空Count
                Count = 0;
                return true;
            }
            else
            {
                return false;
            }
        }

        internal void Clear()
        {
            this.Count = 0;
        }



        int Roundup(int targetCount)
        {
            var dst = defaultSize;
            while (dst < targetCount)
            {
                dst <<= 1;
            }
            return dst;
        }

        private void CheckReSize()
        {
            if (Datas.Length == Count)
            {
                byte[] newDatas = new byte[Count << 1];
                Buffer.BlockCopy(this.Datas, 0, newDatas, 0, Count);
                this.Datas = newDatas;
            }
        }

        private void CheckReSize(int addCount)
        {
            if (Count + addCount > maxSize)
            {
                throw new Exception("data is too large!!!");
            }

            int finalLength = Datas.Length;

            while (Count + addCount > finalLength)
            {
                finalLength <<= 1;
            }

            if (finalLength != Count)
            {
                byte[] newDatas = new byte[finalLength];
                Buffer.BlockCopy(this.Datas, 0, newDatas, 0, Count);
                this.Datas = newDatas;
            }
        }



    }
}
