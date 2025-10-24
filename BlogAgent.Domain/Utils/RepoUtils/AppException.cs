// Copyright (c) Microsoft. All rights reserved.

namespace BlogAgent.Domain.Utils;

internal class AppException : Exception
{
    public AppException() : base()
    {
    }

    public AppException(string message) : base(message)
    {
    }

    public AppException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
