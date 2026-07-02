namespace ECommerce.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class InvalidOrderTransitionException : DomainException
{
    public InvalidOrderTransitionException(string message) : base(message) { }
}
