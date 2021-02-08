using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using DMicroservices.DataAccess.Tests.Models;
using Microsoft.EntityFrameworkCore;

namespace DMicroservices.DataAccess.Tests
{
    public class MasterContext : DbContext
    {
        public MasterContext()
        {

        }
        public MasterContext(DbContextOptions<MasterContext> options)
        {

        }

        public DbSet<City> City { get; set; }
        public DbSet<Person> Person { get; set; }
        public DbSet<Student> Student { get; set; }
        public DbSet<Teacher> Teacher { get; set; }

        public DbSet<Search> Search { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<Person>().HasOne(x => x.City).WithMany(x => x.Persons).HasForeignKey(x => x.ForeignCityId).HasPrincipalKey(x => x.Id);
        }


        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseMySql(Environment.GetEnvironmentVariable("MYSQL_URI"),
                    ServerVersion.AutoDetect(Environment.GetEnvironmentVariable("MYSQL_URI")));
            }
            base.OnConfiguring(optionsBuilder);
        }
    }
}
