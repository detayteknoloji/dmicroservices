using DMicroservices.DataAccess.MongoRepository;
using DMicroservices.DataAccess.Tests.Models;
using DMicroservices.DataAccess.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using StackExchange.Redis;

namespace DMicroservices.DataAccess.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            //using (var repo = MongoRepositoryFactory.CreateMongoRepository<Document>())
            //{
            //    repo.Add(new Document()
            //    {
            //        Id = Guid.NewGuid().ToString(),
            //        Data = new byte[] { 1, 2, 3, 4, 5 }
            //    });

            //    var document = repo.GetAll(x => true).ToList();
            //}

            try
            {
                using (var uow = UnitOfWorkFactory.CreateUnitOfWork<MasterContext>(new UnitOfWorkSettings()
                {
                    ChangeDataCapture = true,
                    ChangedUserPropertyName = "ChangedBy",
                    IdPropertyName = "Id"
                }))
                {
                    var ct = uow.GetRepository<City>().Get(x => x != null);
                  
                    uow.GetRepository<City>().Delete(ct);
                    uow.SaveChanges();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
           

            return;
            
            using (UnitOfWork<MasterContext> uow = new UnitOfWork<MasterContext>())
            {
                var city = new City() { Name = "sivas" };
                var t1 = new Teacher() { Branch = 1, Name = "serhat", City = city };
                var t2 = new Teacher() { Branch = 2, Name = "süha", City = city };

                var s1 = new Student() { StudentNum = 5858, Name = "emre", City = city };
                var s2 = new Student() { StudentNum = 5860, Name = "duhan", City = city };


                uow.GetRepository<City>().Add(city);

                uow.GetRepository<Teacher>().Add(t1);
                uow.GetRepository<Teacher>().Add(t2);

                uow.GetRepository<Student>().Add(s1);
                uow.GetRepository<Student>().Add(s2);

                uow.SaveChanges();

                var persons = uow.GetRepository<City>().Get(x => x.Id.Equals(1), new List<string>() { "Persons" })
                    .Persons;

                foreach (var person in persons)
                {

                    if (person is Student)
                    {

                    }
                    else if (person is Teacher)
                    {
                        var teacher = (Teacher)person;
                    }
                }

            }


            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }
    }
}
