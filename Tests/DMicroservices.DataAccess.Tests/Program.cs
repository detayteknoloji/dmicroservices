using System;
using System.Linq;
using System.Threading;
using DMicroservices.DataAccess.MongoRepository;
using DMicroservices.DataAccess.MongoRepository.Settings;
using DMicroservices.DataAccess.Tests.Models;
using DMicroservices.DataAccess.UnitOfWork;
using DMicroservices.Utils.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DMicroservices.DataAccess.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            using (MongoRepository<ClassModel> mongoRepo = new MongoRepository<ClassModel>(new DatabaseSettings()
            {
                
            }))
            {
                mongoRepo.Add(new ClassModel()
                {
                    Id = 1,
                    Name = "wefgh"
                });
            }

            using (UnitOfWork<MasterContext> uow = new UnitOfWork<MasterContext>("Id",long.Parse("1")))
            {
                var repo = uow.GetRepository<ClassModel>();
                var yy = repo.GetAll(x => true).ToList();
                
                var repoSt = uow.GetRepository<StudentModel>();
                var yy1 = repoSt.GetAll(x => true).ToList();

                //repo.Add(new ClassModel()
                //{
                //    Id = 2,
                //    Name = "test2"
                //});

                //repo.Add(new ClassModel()
                //{
                //    Id = 1,
                //    Name = "tes2"
                //});
                uow.SaveChanges();

            }

           
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }
    }
}
