using System;
using System.Runtime.Serialization;

[Serializable]
internal class DecompressionException : Exception
{
    private string v;
    private object e;

    public DecompressionException()
    {
    }

    public DecompressionException(string message) : base(message)
    {
    }

    public DecompressionException(string v, object e)
    {
        this.v = v;
        this.e = e;
    }

    public DecompressionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    protected DecompressionException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}