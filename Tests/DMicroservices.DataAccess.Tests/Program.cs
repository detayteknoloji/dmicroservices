using DMicroservices.DataAccess.DynamicQuery;
using DMicroservices.DataAccess.Redis;
using DMicroservices.DataAccess.Tests.Models;
using DMicroservices.DataAccess.UnitOfWork;
using DMicroservices.Utils.Logger;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using DMicroservices.DataAccess.Repository;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using DMicroservices.DataAccess.MongoRepository;

namespace DMicroservices.DataAccess.Tests
{
    class Program
    {
        private static RedisList<Search> searchList = new RedisList<Search>("slist");
        static void Main(string[] args)
        {

          
            //Dictionary<string, string> t = new Dictionary<string, string>();
            //for (int i = 0; i < 5000; i++)
            //{
            //    t.Add($"keykeykeykeykey-{i}", $"valvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalvalv-{i}");
            //}

            //RedisManager.Instance.Set(t);



                
                var personList = yy.SelectList(x => x.Id.Equals(2), person => new {person.Name,person.City});
                foreach (var res in personList)
                {
                    
                }

            }

            //Console.ReadLine();
            //SelectDto_Test();
            RedisManager.Instance.Exists("hi");
            //SelectDto_Test();
            //using (var repo = UnitOfWorkFactory.CreateUnitOfWork<MasterContext>())
            //{
            //    DynamicQuery.SelectDto<City, MasterContext> asd = new DynamicQuery.SelectDto<City, MasterContext>();
            //    var cities = asd.GetQueryObject(repo,readonlyRepo:true).ToList();
            //}

            //var testRedisList = new RedisList<Search>("Test");
            //List<Search> searches = new List<Search>();

            //for (int i = 0; i < 10; i++)
            //{
            //    searches.Add(new Search()
            //    {
            //        IntValue = i
            //    });
            //}

            //testRedisList.AddRange(searches);

            //var getSearches = testRedisList.Where(x => x.IntValue == 3).ToList();

            using (var repo = MongoRepositoryFactory.CreateMongoRepository<Document>())
            {
                repo.Add(new Document()
                {
                    Id = Guid.NewGuid().ToString(),
                    Data = new byte[] { 1, 2, 3, 4, 5 }
                });

                var document = repo.GetAll(x => true).ToList();
            }

            //try
            //{
            //    using (var uow = UnitOfWorkFactory.CreateUnitOfWork<MasterContext>())
            //    {
            //        var ct = uow.GetRepository<City>().Get(x => x != null);

            //        //uow.GetRepository<City>().Delete(ct);
            //        //uow.SaveChanges();

            //        var cta = uow.GetReadonlyRepository<City>().Get(x => x != null);

            //    }

            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e);
            //}


            using (UnitOfWork<MasterContext> uow = new UnitOfWork<MasterContext>())
            {
                var city = new City() { Name = "sivas" };
                var t1 = new Teacher() { Branch = 1, Name = "serhat", City = city };
                var t2 = new Teacher() { Branch = 2, Name = "süha", City = city };

                var s1 = new Student() { StudentNum = 5858, Name = "emre", City = city };
                var s2 = new Student() { StudentNum = 5860, Name = "duhan", City = city };
                Random r = new Random();
                var cty = uow.GetRepository<City>().Get(y => y.Id == 1);


                City cc = new City()
                {
                    Id = 1,
                    Name = "test"
                };



                uow.GetRepository<City>().Add(cc);

                uow.SaveChanges();

                //var s3 = new Student()
                //    {StudentNum = r.Next(1,99999), Name = "test", ForeignCityId = cty.Id};


                //uow.GetRepository<City>().Add(city);

                //uow.GetRepository<Teacher>().Add(t1);
                //uow.GetRepository<Teacher>().Add(t2);

                var primaryRepo = uow.GetRepository<Student>();
                getinfo(primaryRepo);

                //uow.GetRepository<Student>().Add(s2);

                //uow.SaveChanges();
                var repo = uow.GetReadonlyRepository<City>();
                getinfo(repo);



                var persons = uow.GetReadonlyRepository<City>().Get(x => x.Id.Equals(1), new List<string>() { "Persons" })
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

            //ElasticLogger.Instance.Info("Info Log", new Dictionary<string, Object>() {
            //        { "No", 1 },
            //        { "Name", "DMicroServices Info" }
            //    });

            //try
            //{
            //    int a = 0;
            //    int b = 10 / a;
            //}
            //catch (Exception ex)
            //{
            //    ElasticLogger.Instance.Error(ex, "Exception Log", new Dictionary<string, Object>() {
            //        { "No", 1 },
            //        { "Name", "DMicroServices Error" }
            //    });
            //}
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }

        private static void getinfo<T>(IRepository<T> repo) where T : class
        {
            using (var command = repo.GetDbContext().Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = "SELECT * FROM xyz.info;";
                command.CommandType = CommandType.Text;

                repo.GetDbContext().Database.OpenConnection();

                using (var result = command.ExecuteReader())
                {
                    while (result.Read())
                    {
                        Console.WriteLine($"data from : {result[0]}");
                    }
                }
            }

        }
        static void SelectDto_Test()
        {
            string selectDtoStringValue = "{\"Filter\":[{\"PropertyName\":\"Id\",\"Operation\":\"IN\",\"PropertyValue\":\"1,2\"},{\"PropertyName\":\"Name\",\"Operation\":\"IN\",\"PropertyValue\":\"Str1,Str2\"}],\"FilterCompareType\":\"AND\",\"FilterCompareTypes\":[{\"Group\":\"gp\",\"Type\":\"OR\"}],\"TakeCount\":1,\"SkipCount\":1}";

            //selectDtoStringValue = "{\"Filter\":[{\"PropertyName\":\"IntValue\",\"Operation\":\"EQ\",\"PropertyValue\":100},{\"PropertyName\":\"BoolValue\",\"Operation\":\"EQ\",\"PropertyValue\":\"false\"},{\"PropertyName\":\"BigIntValue\",\"Operation\":\"EQ\",\"PropertyValue\":\"1001\"},{\"PropertyName\":\"StringValue\",\"Operation\":\"EQ\",\"PropertyValue\":\"Str1\"},{\"PropertyName\":\"ByteValue\",\"Operation\":\"EQ\",\"PropertyValue\":254},{\"PropertyName\":\"DecimalValue\",\"Operation\":\"EQ\",\"PropertyValue\":\"12.3\"},{\"PropertyName\":\"DoubleValue\",\"Operation\":\"EQ\",\"PropertyValue\":\"17.5\"},{\"PropertyName\":\"EnumValue\",\"Operation\":\"EQ\",\"PropertyValue\":\"2\"},{\"PropertyName\":\"SmallIntValue\",\"Operation\":\"EQ\",\"PropertyValue\":\"10\"}],\"FilterCompareType\":\"AND\",\"FilterCompareTypes\":[{\"Group\":\"gp\",\"Type\":\"OR\"}]}";

            SelectDto<City, MasterContext> selectDtoString = JsonConvert.DeserializeObject<SelectDto<City, MasterContext>>(selectDtoStringValue);


            var ex = selectDtoString.GetExpression();


            var y = 1;
            //using (UnitOfWork<MasterContext> uow = new UnitOfWork<MasterContext>())
            //{
            //    uow.GetRepository<Search>().Add(new Search()
            //    {
            //        StringValue = "Str1",
            //        IntValue = 100,
            //        BigIntValue = 1001,
            //        SmallIntValue = 10,
            //        BoolValue = false,
            //        ByteValue = 254,
            //        DateTimeValue = DateTime.Now.AddDays(-1),
            //        DecimalValue = 12.3m,
            //        DoubleValue = 17.5,
            //        EnumValue = Number.Two,
            //        GuidValue = Guid.NewGuid(),
            //        GuidNullableValue = Guid.NewGuid(),
            //        IntNullableValue = null
            //    });

            //    uow.GetRepository<Search>().Add(new Search()
            //    {
            //        StringValue = "Str2",
            //        IntValue = 101,
            //        BigIntValue = 1002,
            //        SmallIntValue = 11,
            //        BoolValue = true,
            //        ByteValue = 255,
            //        DateTimeValue = DateTime.Now,
            //        DecimalValue = 16.3m,
            //        DoubleValue = 10.5,
            //        EnumValue = Number.One,
            //        GuidValue = Guid.NewGuid(),
            //        GuidNullableValue = null,
            //        IntNullableValue = 1
            //    });

            //    uow.SaveChanges();
            //var allData = uow.GetRepository<Search>().GetAll(x => x.BoolValue == bool.Parse("false") && x.StringValue == "Str1").ToList();

            //var queryResult = selectDtoString.GetQueryObject(uow).ToList();
        }
    }
}

