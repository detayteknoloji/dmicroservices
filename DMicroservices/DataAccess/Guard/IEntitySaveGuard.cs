using System;
using Microsoft.EntityFrameworkCore;

namespace DMicroservices.DataAccess.Guard
{
    /// <summary>
    /// UnitOfWork.SaveChanges öncesinde, yazılacak  yani added veya motified yapılacak her entity için savechanges yapılırkenkı çalıştırılacak olan doğrulama sınıfının intrerfacesidir. Bu sınıfı uygulayan yer kendi kurallarını <see cref="UnitOfWorkGuardRegistry.Register"/> ile kaydeder.
    /// Eğerki kuraldan geçmezse exception atılır.
    /// </summary>
    public interface IEntitySaveGuard
    {
        /// <param name="entity">Yazılacak entity.</param>
        /// <param name="state">Entitynin ChangeTracker durumunu belirtir added veya motified.</param>
        /// <param name="filterColumnName">UnitOfWork filtre kolonu adı (örneğin "CompanyNo"); filtresiz uowda null.</param>
        /// <param name="filterColumnValue">UnitOfWork filtre kolonu değeri; filtresiz uowda null.</param>
        /// <param name="dbContextType">Uowun DbContext tipi.</param>
        void Validate(object entity, EntityState state, string filterColumnName, object filterColumnValue, Type dbContextType);
    }
}
