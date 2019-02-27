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
        public TypeIdRepeatException(int typeId)
        {
            this._typeId = typeId;
        }

        public override string Message
        {
            get
            {
                return string.Format("The typeId '{0}' has been regist multiply times", this._typeId);
            }
        }
    }
}
