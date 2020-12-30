using System;
using System.Collections.Generic;
using System.Text;
using DMicroservices.DataAccess.MongoRepository.Interfaces;
using DMicroservices.DataAccess.MongoRepository.Settings;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace DMicroservices.DataAccess.UnitOfWork
{
    public static class UnitOfWorkFactory
    {
        public static UnitOfWork<T> CreateUnitOfWork<T>() where T : DbContext
        {
            return new UnitOfWork<T>();
        }
        public static UnitOfWork<T> CreateUnitOfWork<T>(object companyNo) where T : DbContext
        {
            if (companyNo != null)
                return new UnitOfWork<T>("CompanyNo", companyNo);
            return new UnitOfWork<T>();
        }
    }
}
