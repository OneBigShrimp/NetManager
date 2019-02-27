using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace MyNetManager
{
    internal partial class SerializeControl
    {

        class DeserializeCell : IDeserializable
        {
            DeserializeBase _des;

            internal DeserializeCell(DeserializeBase des)
            {
                this._des = des;
            }

            public void Deserialize(MyStream stream, object target)
            {
                _des.Deserialize(stream, target);
            }

            internal object GetDeserializeValue(MyStream stream)
            {
                return _des.GetDeserializeValue(stream);
            }

        }

        class DeserializeUnit : IDeserializable
        {
            DeserializeBase[] _desArray;

            public DeserializeUnit(DeserializeBase[] desArray)
            {
                this._desArray = desArray;
            }

            public void Deserialize(MyStream stream, object target)
            {
                for (int i = 0; i < _desArray.Length; i++)
                {
                    _desArray[i].Deserialize(stream, target);
                }
            }
        }


        abstract class DeserializeBase
        {
            private FieldInfo Field;

            internal DeserializeBase() { }

            internal DeserializeBase(FieldInfo field)
            {
                this.Field = field;
            }

            internal abstract object GetDeserializeValue(MyStream stream);

            internal void Deserialize(MyStream stream, object target)
            {
                object value = GetDeserializeValue(stream);
                Field.SetValue(target, value);
            }
        }
        class DeserializeInt : DeserializeBase
        {
            internal DeserializeInt(FieldInfo field)
                : base(field)
            {
            }
            internal override object GetDeserializeValue(MyStream stream)
            {
                return stream.ReadInt();
            }
        }
        class DeserializeString : DeserializeBase
        {
            internal DeserializeString(FieldInfo field)
                : base(field)
            {
            }
            internal override object GetDeserializeValue(MyStream stream)
            {
                return stream.ReadString();
            }
        }
        class DeserializeByte : DeserializeBase
        {
            internal DeserializeByte(FieldInfo field)
                : base(field)
            {
            }
            internal override object GetDeserializeValue(MyStream stream)
            {
                return stream.ReadByte();
            }
        }
        class DeserializeFloat : DeserializeBase
        {
            internal DeserializeFloat(FieldInfo field)
                : base(field)
            {
            }
            internal override object GetDeserializeValue(MyStream stream)
            {
                return stream.ReadFloat();
            }
        }
        class DeserializeObj : DeserializeBase
        {
            Type fieldType;
            internal DeserializeObj(FieldInfo field)
                : base(field)
            {
                fieldType = field.FieldType;
            }
            internal override object GetDeserializeValue(MyStream stream)
            {
                return SerializeControl.Instance._Deserialize(stream, fieldType);
            }
        }

        class DeserializeArray : DeserializeBase
        {
            Type elementType;

            internal DeserializeArray(FieldInfo field)
                : base(field)
            {
                elementType = field.FieldType.GetElementType();
            }

            internal override object GetDeserializeValue(MyStream stream)
            {
                byte arrayLength = stream.ReadByte();
                Array array = Array.CreateInstance(elementType, arrayLength);
                for (int i = 0; i < arrayLength; i++)
                {
                    array.SetValue(SerializeControl.Instance._Deserialize(stream, elementType), i);
                }
                return array;
            }
        }
    }
}