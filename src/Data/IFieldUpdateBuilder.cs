using System.Linq.Expressions;

namespace Data;

public interface IFieldUpdateBuilder<T>
{
    IFieldUpdateBuilder<T> Set<TField>(Expression<Func<T, TField>> field, TField value);
}
