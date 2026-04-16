namespace GestionITVPro.Extensions;

using C = CSharpFunctionalExtensions;

public static class MaybeExtensions 
{
    public static C.Result<T, TError> ToResult<T, TError>(this C.Maybe<T> maybe, TError error)
        where T : class
        where TError : GestionITVPro.Error.Common.DomainError 
    { 
        return maybe.HasValue
            ? C.Result.Success<T, TError>(maybe.Value)
            : C.Result.Failure<T, TError>(error);
    } // Cierra el primer método

    public static C.Result<T, TError> ToResult<T, TError>(this C.Maybe<T> maybe, Func<TError> errorFactory)
        where T : class
        where TError : GestionITVPro.Error.Common.DomainError 
    {
        return maybe.HasValue
            ? C.Result.Success<T, TError>(maybe.Value)
            : C.Result.Failure<T, TError>(errorFactory()); 
    } // Cierra el segundo método
} // Cierra la clase

// No necesitas llaves para el namespace porque estás usando "File-scoped namespace" (el punto y coma al principio).