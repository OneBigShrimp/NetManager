using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Reflection;

namespace MyNetManager
{
    /// <summary>
    /// 序列化控制类,可序列化的对象必须实现IProtocol接口,使用时要先注册Type和Id的对应关系,
    /// 目前基本类型支持byte,int,float,string,自定义结构(实现ISerObj接口)和数组(内部可以放基本结构,也可以放自定义结构,暂不支持数组中放数组int[][]和多维数组int[,])
    /// </summary>  
    internal partial class SerializeControl
    {
        internal readonly static SerializeControl Instance = new SerializeControl();

        Dictionary<Type, int> type2Id = new Dictionary<Type, int>();
        Dictionary<int, Type> id2Type = new Dictionary<int, Type>();

        Dictionary<Type, ISerializable> serializeMap = new Dictionary<Type, ISerializable>();
        Dictionary<Type, IDeserializable> deserializeMap = new Dictionary<Type, IDeserializable>();



        internal int CurProtocolId { private set; get; }

        private SerializeControl()
        {
            AddBaseSerDesPair();
        }

        /// <summary>
        /// 注册类型和类型Id的对应关系
        /// </summary>
        /// <param name="type"></param>
        /// <param name="typeId"></param>
        internal void Regist(Type type)
        {
            int typeId = Utils.HashString(type.Name);
            if (!typeof(IProtocol).IsAssignableFrom(type))
            {
                throw new RegistTypeException(type);
            }
            if (id2Type.ContainsKey(typeId))
            {
                throw new TypeIdRepeatException(typeId, type, id2Type[typeId]);
            }

            AddClass(type);
            type2Id.Add(type, typeId);
            id2Type.Add(typeId, type);
        }


        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="target">序列化目标</param>
        internal void Serialize(MyStream stream, IProtocol target)
        {
            Type targetType = target.GetType();
            if (!serializeMap.ContainsKey(targetType))
            {
                throw new NoRegistException(targetType);
            }

            int lengthPos = stream.Oct.Count + 4;

            stream.Write(type2Id[targetType]);
            //空出4个字节留给协议长度
            stream.Write(0);
            Serialize_Internal(stream, target);

            int length = stream.Oct.Count - lengthPos - 4;
            stream.WriteProtocolLength(lengthPos, length);

        }

        /// <summary>
        /// 将一个byte数组反序列化为一组IProtocol对象
        /// </summary>
        /// <returns></returns>
        internal void Deserialize(MyStream stream, Queue<IProtocol> protocols)
        {
            for (int i = 0; i < 100; i++)
            {
                if (!stream.CanAnalyse)
                {//流的内容太短,无法解析类型和长度
                    return;
                }
                IProtocol proto = Deserialize_Internal(stream);
                if (proto == null)
                {//协议长度不够,要和下次一起解析
                    return;
                }
                else
                {
                    protocols.Enqueue(proto);
                }
            }
        }



        internal void Serialize_Internal(MyStream stream, object target)
        {
            Type targetType = target.GetType();
            ISerializable ser = serializeMap[targetType];
            ser.Serialize(stream, target);
        }


        internal IProtocol Deserialize_Internal(MyStream stream)
        {
            int oldPos = stream.Position;

            this.CurProtocolId = stream.ReadInt();
            Type targetType = id2Type[this.CurProtocolId];
            int protocolLength = stream.ReadInt();
            int finalPos = stream.Position + protocolLength;
            if (stream.RemainCount < protocolLength)
            {
                //长度不够,回滚Position
                stream.Position = oldPos;
                return null;
            }

            IProtocol result = _Deserialize(stream, targetType) as IProtocol;
            if (stream.Position != finalPos)
            {
                throw new LengthException();
                //rs.Position = startPos + protocolLength;
            }
            return result;
        }



        /// <summary>
        /// 根据类型反序列化,会通过反射创建对象,并将其返回
        /// </summary>
        /// <returns></returns>
        internal object _Deserialize(MyStream stream, Type type)
        {
            if (!deserializeMap.ContainsKey(type))
            {
                throw new Exception("Type has not been regist : " + type);
            }

            if (typeof(ISerObj).IsAssignableFrom(type))
            {
                object result = type.Assembly.CreateInstance(type.FullName);
                IDeserializable des = deserializeMap[type];
                des.Deserialize(stream, result);
                return result;
            }
            else
            {
                DeserializeCell des = deserializeMap[type] as DeserializeCell;
                return des.GetDeserializeValue(stream);
            }
        }

        /// <summary>
        /// 添加需要被序列化的对象类型
        /// </summary>
        /// <param name="type"></param>
        void AddClass(Type type)
        {
            if (serializeMap.ContainsKey(type))
            {
                return;
            }

            if (!typeof(ISerObj).IsAssignableFrom(type))
            {
                throw new Exception("AddClass type must be a subclass of interface 'ISerObj'");
            }

            List<Type> newAddTyps = new List<Type>();

            FieldInfo[] allField = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            SerializeBase[] sbArray = new SerializeBase[allField.Length];
            DeserializeBase[] dbArray = new DeserializeBase[allField.Length];
            for (int i = 0; i < allField.Length; i++)
            {
                Type fieldType = allField[i].FieldType;
                if (fieldType == typeof(int))
                {
                    sbArray[i] = new SerializeInt(allField[i]);
                    dbArray[i] = new DeserializeInt(allField[i]);
                }
                else if (fieldType == typeof(string))
                {
                    sbArray[i] = new SerializeString(allField[i]);
                    dbArray[i] = new DeserializeString(allField[i]);
                }
                else if (fieldType == typeof(byte))
                {
                    sbArray[i] = new SerializeByte(allField[i]);
                    dbArray[i] = new DeserializeByte(allField[i]);
                }
                else if (fieldType == typeof(float))
                {
                    sbArray[i] = new SerializeFloat(allField[i]);
                    dbArray[i] = new DeserializeFloat(allField[i]);
                }
                else if (typeof(ISerObj).IsAssignableFrom(fieldType))
                {
                    sbArray[i] = new SerializeObj(allField[i]);
                    dbArray[i] = new DeserializeObj(allField[i]);
                    if (!newAddTyps.Contains(fieldType))
                    {
                        newAddTyps.Add(fieldType);
                    }
                    //这里没有调用AddClass,而是添加到一个列表中,在serializeMap中成功添加后,才处理递归AddClass
                    //防止在发生类型嵌套(A包含B,B包含A)时出现AddClass递归死循环,
                    //AddClass(fieldType);
                }
                else if (fieldType.IsArray)
                {
                    sbArray[i] = new SerializeArray(allField[i]);
                    dbArray[i] = new DeserializeArray(allField[i]);
                    Type eleType = fieldType.GetElementType();
                    if (typeof(ISerObj).IsAssignableFrom(eleType))
                    {
                        if (!newAddTyps.Contains(eleType))
                        {
                            newAddTyps.Add(eleType);
                        }
                    }
                }
                else
                {
                    throw new Exception("Can't serialize type -->" + allField[i].FieldType.ToString());
                }
            }
            ISerializable ser;
            IDeserializable des;
            if (sbArray.Length == 1)
            {
                ser = new SerializeCell(sbArray[0]);
                des = new DeserializeCell(dbArray[0]);
            }
            else
            {
                ser = new SerializeUnit(sbArray);
                des = new DeserializeUnit(dbArray);
            }
            serializeMap.Add(type, ser);
            deserializeMap.Add(type, des);

            for (int i = 0; i < newAddTyps.Count; i++)
            {
                AddClass(newAddTyps[i]);
            }
        }

        private void AddBaseSerDesPair()
        {
            serializeMap.Add(typeof(byte), new SerializeCell(new SerializeByte(null)));
            serializeMap.Add(typeof(int), new SerializeCell(new SerializeInt(null)));
            serializeMap.Add(typeof(float), new SerializeCell(new SerializeFloat(null)));
            serializeMap.Add(typeof(string), new SerializeCell(new SerializeString(null)));

            deserializeMap.Add(typeof(byte), new DeserializeCell(new DeserializeByte(null)));
            deserializeMap.Add(typeof(int), new DeserializeCell(new DeserializeInt(null)));
            deserializeMap.Add(typeof(float), new DeserializeCell(new DeserializeFloat(null)));
            deserializeMap.Add(typeof(string), new DeserializeCell(new DeserializeString(null)));
        }



    }

    public interface IProtocol : ISerObj
    {
        void Process(ILinker linker, object args);
    }

    public interface ISerObj { }


    internal interface ISerializable
    {
        void Serialize(MyStream stream, object target);
    }

    internal interface IDeserializable
    {
        void Deserialize(MyStream stream, object target);
    }

}