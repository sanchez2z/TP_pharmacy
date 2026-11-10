using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace TP_pharmacy.Data;

// КОНФЛИКТЫ ЗАПИСИ, СВЯЗАННЫЕ С ДЕЙСТВИЯМИ ПОЛЬЗОВАТЕЛЯ
public class DatabaseExceptionFilter(ITempDataDictionaryFactory tempDataFactory, ILogger<DatabaseExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var message = context.Exception switch
        {
            DbUpdateConcurrencyException => "Запись изменена другим пользователем. Обновите данные и повторите операцию.",
            DbUpdateException { InnerException: PostgresException { SqlState: "23503" } } => "Операция невозможна: запись связана с другими данными или уже удалена.",
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => "Запись с такими уникальными данными уже существует.",
            _ => null
        };
        if (message == null) return;
        logger.LogWarning("Database write conflict: {ExceptionType}", context.Exception.GetType().Name);
        tempDataFactory.GetTempData(context.HttpContext)["DatabaseError"] = message;
        context.Result = new LocalRedirectResult(context.HttpContext.Request.PathBase + context.HttpContext.Request.Path);
        context.ExceptionHandled = true;
    }
}
