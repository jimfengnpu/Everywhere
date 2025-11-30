namespace Everywhere.Mac.Interop;

public enum AXError
{
    Success = 0,
    Failure = -25200,
    IllegalArgument = -25201,
    InvalidUIElement = -25202,
    InvalidUIElementObserver = -25203,
    CannotComplete = -25204,
    AttributeUnsupported = -25205,
    ActionUnsupported = -25206,
    NotificationUnsupported = -25207,
    NotImplemented = -25208,
    NotificationAlreadyRegistered = -25209,
    NotificationNotRegistered = -25210,
    APIDisabled = -25211,
    NoValue = -25212,
    ParameterizedAttributeUnsupported = -25213,
    NotEnoughPrecision = -25214
}