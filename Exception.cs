using System;
using System.Collections.Generic;
using System.Text;

namespace MyNetManager
{
    public class LengthException : Exception
    {
        public override string Message
        {
            get
            {
                return "Protocol length not match! Protocol Id : " + SerializeControl.Instance.CurProtocolId;
            }
        }
    }

    public class NoRegistException : Exception
    {
        Type _needType;
        public NoRegistException(Type needType)
        {
            this._needType = needType;
        }

        public override string Message
        {
            get
            {
                return "Protocol type has not regist :" + this._needType;
            }
        }
    }


    public class RegistTypeException : Exception
    {
        Type _regType;
        public RegistTypeException(Type regType)
        {
            this._regType = regType;
        }

        public override string Message
        {
            get
            {
                return string.Format("The type '{0}' must be a subclass of 'IProtocol'!", this._regType);
            }
        }
    }

    public class TypeIdRepeatException : Exception
    {
        int _typeId;
        Type _tp1;
        Type _tp2;
        public TypeIdRepeatException(int typeId, Type tp1, Type tp2)
        {
            this._typeId = typeId;
            this._tp1 = tp1;
            this._tp2 = tp2;
        }

        public override string Message
        {
            get
            {
                return string.Format("Two type: '{0}' and '{1}' has the same hashvalue : {2}, you can change one of the name",
                    this._typeId, this._tp1.Name, this._tp2.Name);
            }
        }
    }
}
