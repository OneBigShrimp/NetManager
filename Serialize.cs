using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MyNetManager
{
    internal partial class SerializeControl
    {

        class SerializeCell : ISerializable
        {
            SerializeBase _ser;

            public SerializeCell(SerializeBase ser)
            {
                this._ser = ser;
            }

            public void Serialize(MyStream stream, object target)
            {
                _ser.Serialize(stream, target);
            }
        }


        class SerializeUnit : ISerializable
        {
            SerializeBase[] _serArray;

            public SerializeUnit(SerializeBase[] serArray)
            {
                this._serArray = serArray;
            }

            public void Serialize(MyStream stream, object target)
            {
                for (int i = 0; i < _serArray.Length; i++)
                {
                    _serArray[i].Serialize(stream, target);
                }
            }
        }





        abstract class SerializeBase
        {
            /// <summary>
            /// 需要被序列化的字段所在的属性,如果为null,则target就是要序列化的值
            /// </summary>
            protected FieldInfo Field;
            public SerializeBase(FieldInfo field)
            {
                this.Field = field;
            }

            protected object GetSerializeValue(object target)
            {
                return Field == null ? target : Field.GetValue(target);
            }

            public abstract void Serialize(MyStream stream, object target);
        }
        class SerializeInt : SerializeBase
        {
            public SerializeInt(FieldInfo field)
                : base(field)
            {
            }

            public override void Serialize(MyStream stream, object target)
            {
                stream.Write((int)GetSerializeValue(target));
            }
        }
        class SerializeString : SerializeBase
        {

            public SerializeString(FieldInfo field)
                : base(field)
            {
            }

            public override void Serialize(MyStream stream, object target)
            {
                stream.Write((string)GetSerializeValue(target));
            }
        }
        class SerializeByte : SerializeBase
        {
            public SerializeByte(FieldInfo field)
                : base(field)
            {
            }

            public override void Serialize(MyStream stream, object target)
            {
                stream.Write((byte)GetSerializeValue(target));
            }

        }
        class SerializeFloat : SerializeBase
        {
            public SerializeFloat(FieldInfo field)
                : base(field)
            {
            }

            public override void Serialize(MyStream stream, object target)
            {
                stream.Write((float)GetSerializeValue(target));
            }

        }
        class SerializeObj : SerializeBase
        {
            public SerializeObj(FieldInfo field)
                : base(field)
            {
            }
            public override void Serialize(MyStream stream, object target)
            {
                SerializeControl.Instance.Serialize_Internal(stream, Field.GetValue(target));
            }
        }

        class SerializeArray : SerializeBase
        {
            Type elementType;
            public SerializeArray(FieldInfo field)
                : base(field)
            {
                elementType = field.FieldType.GetElementType();
            }

            public override void Serialize(MyStream stream, object target)
            {
                Array array = Field.GetValue(target) as Array;
                byte length = (byte)array.Length;
                stream.Write(length);

                for (int i = 0; i < length; i++)
                {
                    SerializeControl.Instance.Serialize_Internal(stream, array.GetValue(i));
                }
            }
        }

    }
}