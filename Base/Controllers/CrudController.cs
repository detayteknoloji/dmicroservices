using System.Linq;
using DMicroservices.DataAccess.DynamicQuery;
using DMicroservices.DataAccess.UnitOfWork;
using DMicroservices.Utils.Extensions;
using DMicroservices.Utils.ObjectMapper; 
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DMicroservices.Base.Controllers
{
    /// <summary>
    /// CrudController is a generic crud operations based WebApi controller.
    /// This controller contains only base CRUD operations.
    /// </summary>
    public class CrudController<T, D> : Controller
        where D : DbContext
        where T : class
    {

        [HttpGet]
        [Route("{id}")]
        public IActionResult Get(long id)
        {
            using (var uow = new UnitOfWork<D>())
            {
                var repository = uow.GetRepository<T>();
                T item = repository.GetAll(id.GetIdentifierExpression<T>())
                    .Include(repository.GetDbContext().GetIncludePaths(typeof(T))).FirstOrDefault();

                if (item == null)
                    return StatusCode(404);
                return Json(item);
            }
        }

        [HttpPost]
        [Route("DynamicQuery")]
        public IActionResult DynamicQuery([FromBody] SelectDto<T, D> selectDto)
        {
            using (var uow = new UnitOfWork<D>())
            {
                var repository = uow.GetRepository<T>();
                return Json(repository.GetAll(selectDto.GetExpression())
                    .Include(repository.GetDbContext().GetIncludePaths(typeof(T))).ToList());
            }
        }


        [HttpPut]
        public IActionResult Put([FromBody]T item)
        {
            using (var uow = new UnitOfWork<D>())
            {
                uow.GetRepository<T>().Add(item);
                if (uow.SaveChanges() > 0)
                    return Json(item);
                return StatusCode(500);
            }
        }

        [HttpDelete]
        [Route("{id}")]
        public IActionResult Delete(long id)
        {
            using (var uow = new UnitOfWork<D>())
            {
                T item = uow.GetRepository<T>().Get(id.GetIdentifierExpression<T>());
                if (item == null)
                    return StatusCode(404);

                uow.GetRepository<T>().Delete(item);
                return StatusCode(uow.SaveChanges() > 0 ? 200 : 500);
            }
        }

        [HttpPost]
        [Route("{id}")]
        public IActionResult Update(long id, [FromBody] T updateItem)
        {
            using (var uow = new UnitOfWork<D>())
            {
                T item = uow.GetRepository<T>().Get(id.GetIdentifierExpression<T>());
                if (item == null)
                    return StatusCode(404);

                ObjectMapper.MapExclude(item, updateItem, new string[] { typeof(T).GetIdentifierColumnName() });
                uow.GetRepository<T>().Update(item);
                return StatusCode(uow.SaveChanges() > 0 ? 200 : 500);
            }
        }

    }
}
