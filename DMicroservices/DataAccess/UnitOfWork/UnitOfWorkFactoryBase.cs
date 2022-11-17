using System;
using System.Collections.Generic;
using System.Text;
using DMicroservices.DataAccess.MongoRepository.Interfaces;
using DMicroservices.DataAccess.MongoRepository.Settings;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace DMicroservices.DataAccess.UnitOfWork
{
    public abstract class UnitOfWorkFactoryBase
    {
        public virtual UnitOfWork<T> CreateUnitOfWork<T>() where T : DbContext, ICustomDbContext
        {
            return new UnitOfWork<T>();
        }
        public virtual UnitOfWork<T> CreateUnitOfWork<T>(object companyNo) where T : DbContext, ICustomDbContext
        {
            if (companyNo != null)
                return new UnitOfWork<T>("CompanyNo", companyNo);
            return new UnitOfWork<T>();
        }

        public virtual UnitOfWork<T> CreateUnitOfWork<T>(string filterPropertyName, object filterPropertyValue) where T : DbContext, ICustomDbContext
        {
            return new UnitOfWork<T>(filterPropertyName, filterPropertyValue);
        }

        public virtual UnitOfWork<T> CreateUnitOfWork<T>(UnitOfWorkSettings unitOfWorkSettings) where T : DbContext, ICustomDbContext
        {
            return new UnitOfWork<T>(unitOfWorkSettings);
        }

        public virtual UnitOfWork<T> CreateUnitOfWork<T>(object companyNo, UnitOfWorkSettings unitOfWorkSettings) where T : DbContext, ICustomDbContext
        {
            if (companyNo != null)
                return new UnitOfWork<T>("CompanyNo", companyNo, unitOfWorkSettings);
            return new UnitOfWork<T>(unitOfWorkSettings);
        }

        public virtual UnitOfWork CreateUnitOfWork(Type unitOfWorkType)
        {
            return new UnitOfWork(unitOfWorkType);
        }

        public virtual UnitOfWork CreateUnitOfWork(object companyNo, Type unitOfWorkType)
        {
            if (companyNo != null)
                return new UnitOfWork("CompanyNo", companyNo, unitOfWorkType);
            return new UnitOfWork(unitOfWorkType);
        }

        public virtual UnitOfWork CreateUnitOfWork(string filterPropertyName, object filterPropertyValue, Type unitOfWorkType)
        {
            return new UnitOfWork(filterPropertyName, filterPropertyValue, unitOfWorkType);
        }

        public virtual UnitOfWork CreateUnitOfWork(UnitOfWorkSettings unitOfWorkSettings, Type unitOfWorkType)
        {
            return new UnitOfWork(unitOfWorkSettings, unitOfWorkType);
        }

        public virtual UnitOfWork CreateUnitOfWork(object companyNo, UnitOfWorkSettings unitOfWorkSettings, Type unitOfWorkType)
        {
            if (companyNo != null)
                return new UnitOfWork("CompanyNo", companyNo, unitOfWorkSettings, unitOfWorkType);
            return new UnitOfWork(unitOfWorkSettings, unitOfWorkType);
        }
    }
}
