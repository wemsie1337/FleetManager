namespace FleetManager.Services;

/// <summary>Ошибка бизнес-валидации, текст которой можно безопасно показывать пользователю.</summary>
public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
