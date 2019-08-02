﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using GrooveMessengerDAL.Entities;
using GrooveMessengerDAL.Models;
using GrooveMessengerDAL.Repositories.Interface;
using GrooveMessengerDAL.Services.Interface;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using static GrooveMessengerDAL.Entities.UserInfoEntity;

namespace GrooveMessengerDAL.Repositories
{
    public class GenericRepository<TEntity, TKey, TContext> : IGenericRepository<TEntity, TKey, TContext>
        where TEntity : BaseEntity<TKey> where TContext : DbContext
    {
        protected readonly DbContext DbContext;
        protected readonly IUserResolverService UserResolverService;

        private DbSet<TEntity> _entity;

        public GenericRepository(TContext dbContext, IUserResolverService userResolverService)
        {
            DbContext = dbContext;
            UserResolverService = userResolverService;
        }

        protected DbSet<TEntity> Entity => _entity ?? (_entity = DbContext.Set<TEntity>());

        public IQueryable<TEntity> GetAll()
        {
            return Entity.AsNoTracking().Where(x => x.Deleted == null || !x.Deleted.Value);
        }

        public Task<IQueryable<TEntity>> GetAllAsync()
        {
            var result = Task.Run(() => { return GetAll(); });
            return result;
        }

        public TEntity GetSingle(TKey entityId)
        {
            return Entity.AsNoTracking().FirstOrDefault(x =>
                x.Id.Equals(entityId) && (x.Deleted == null || !x.Deleted.Value));
        }

        public Task<TEntity> GetSingleAsync(TKey entityId)
        {
            var result = Task.Run(() => { return GetSingle(entityId); });
            return result;
        }

        public IQueryable<TEntity> GetBy(Expression<Func<TEntity, bool>> predicate)
        {
            return Entity.AsNoTracking().Where(x => x.Deleted == null || !x.Deleted.Value)
                .Where(predicate);
        }

        public Task<IQueryable<TEntity>> GetByAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var result = Task.Run(() => { return GetBy(predicate); });
            return result;
        }

        public IQueryable<TEntity> FindAll()
        {
            return Entity.Where(x => x.Deleted == null || !x.Deleted.Value);
        }

        public Task<IQueryable<TEntity>> FindAllAsync()
        {
            var result = Task.Run(() => { return FindAll(); });
            return result;
        }

        public TEntity FindSingle(TKey entityId)
        {
            return Entity.FirstOrDefault(x =>
                x.Id.Equals(entityId) && (x.Deleted == null || !x.Deleted.Value));
        }

        public Task<TEntity> FindSingleAsync(TKey entityId)
        {
            var result = Task.Run(() => { return FindSingle(entityId); });
            return result;
        }

        public IQueryable<TEntity> FindBy(Expression<Func<TEntity, bool>> predicate)
        {
            return Entity.Where(x => x.Deleted == null || !x.Deleted.Value)
                .Where(predicate);
        }

        public Task<IQueryable<TEntity>> FindByAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var result = Task.Run(() => { return FindBy(predicate); });
            return result;
        }

        public EntityEntry<TEntity> Add(TEntity entity)
        {
            return Entity.Add(entity);
        }

        public Task<EntityEntry<TEntity>> AddAsync(TEntity entity)
        {
            var result = Entity.AddAsync(entity);
            return result;
        }

        public void Delete(TKey entityId)
        {
            var storedEntity = Entity.FirstOrDefault(x =>
                x.Id.Equals(entityId) && (x.Deleted == null || !x.Deleted.Value));
            if (storedEntity == null) return;
            storedEntity.Deleted = true;
            Entity.Attach(storedEntity).State = EntityState.Modified;
        }

        public Task DeleteAsync(TKey entityId)
        {
            var result = Task.Run(() => { Delete(entityId); });
            return result;
        }

        public void Edit(TEntity entity)
        {
            var storedEntity = Entity.Attach(entity);
            storedEntity.State = EntityState.Modified;
        }

        public Task EditAsync(TEntity entity)
        {
            var result = Task.Run(() => { Edit(entity); });
            return result;
        }

        public bool CheckExistingById(TKey id)
        {
            var result = Entity.AsNoTracking().Any(x => x.Id.Equals(id));
            return result;
        }

        public IQueryable<TEntity> ExecuteReturedStoredProcedure(string storedProcedureName,
            params SqlParameter[] parameters)
        {
            var wrapperQuery = BuildSqlExecutionStatement(storedProcedureName, parameters);
            return Entity.FromSql(wrapperQuery, parameters);
        }

        public int ExecuteStoredProcedure(string storedProcedureName, params SqlParameter[] parameters)
        {
            var wrapperQuery = BuildSqlExecutionStatement(storedProcedureName, parameters);
            return DbContext.Database.ExecuteSqlCommand(wrapperQuery, parameters);
        }

        public List<TResult> ExecuteReturedStoredProcedure<TResult>(string storedProcedureName,
            params SqlParameter[] parameters)
        {
            var parameterNames = "";
            var parameterDeclaration = "";
            var parameterInput = "";
            var parameterCount = parameters.Count() - 1;
            for (var i = 0; i <= parameterCount; i++)
            {
                parameterNames += $"@{parameters[i].ParameterName}";
                parameterDeclaration += $"@{parameters[i].ParameterName} {parameters[i].SqlDbType.ToString()}";
                if (parameters[i].SqlDbType.ToString().ToLower().Contains("char"))
                    parameterDeclaration += $"({(parameters[i].Size <= 0 ? "MAX" : parameters[i].Size.ToString())})";
                parameterInput +=
                    $"@{parameters[i].ParameterName} = N'{parameters[i].Value?.ToString().Replace("'", "''")}'";
                if (i < parameterCount)
                {
                    parameterNames += ", ";
                    parameterDeclaration += ", ";
                    parameterInput += ", ";
                }
            }

            var commandText =
                $"exec sp_executesql N'EXECUTE {storedProcedureName} {parameterNames}', N'{parameterDeclaration}',{parameterInput}";
            if (storedProcedureName== "[dbo].[usp_Message_GetUnreadMessageAmount]") Debug.Write("EXECUTE:______________ " + commandText);
            using (var command = DbContext.Database.GetDbConnection().CreateCommand())
            {
                command.CommandText = commandText;
                if (command.Connection.State == ConnectionState.Closed) command.Connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    var results = CreateList<TResult>(reader);
                    if (storedProcedureName == "[dbo].[usp_Message_GetUnreadMessageAmount]") Debug.Write("EXECUTE Result:______________ " + results[0].ToString());
                    return results;
                }
            }
        }

        private string BuildSqlExecutionStatement(string storedProcedureName, SqlParameter[] parameters)
        {
            var spSignature = new StringBuilder();

            spSignature.AppendFormat("EXECUTE {0}", storedProcedureName);
            var length = parameters.Count() - 1;


            for (var i = 0; i < parameters.Count(); i++)
            {
                spSignature.AppendFormat(" @{0}", parameters[i].ParameterName);
                if (i != length) spSignature.Append(",");
            }

            return spSignature.ToString();
        }

        private List<TResult> CreateList<TResult>(IDataReader reader)
        {
            var results = new List<TResult>();
            var properties = typeof(TResult).GetProperties();

            Debug.Write("EXECUTE Datareader:______________ " + reader);
            while (reader.Read())
            {
                if (typeof(TResult).IsPrimitive || typeof(TResult) == typeof(string) || typeof(TResult) == typeof(int))
                {
                    Debug.Write("EXECUTE Property Reader0:______________ " + (TResult)reader[0]);
                    var value = (TResult) reader[0];
                    results.Add(value);
                    continue;
                }

                var item = Activator.CreateInstance<TResult>();
                foreach (var property in properties)
                    try
                    {
                        if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                        {
                            var attrs = Attribute.GetCustomAttributes(property);
                            if (attrs.Any(x => x is MapBy))
                            {
                                var enumType = typeof(StatusName);
                                var currentValue = reader[property.Name];
                                var enumValue = Enum.Parse(enumType, currentValue.ToString());
                                var convertedValue = enumValue.ToString();
                                property.SetValue(item, convertedValue);
                            }
                            else
                            {
                                var convertTo = Nullable.GetUnderlyingType(property.PropertyType) ??
                                                property.PropertyType;
                                Debug.Write("EXECUTE Property Value:______________ " + reader[property.Name]);
                                property.SetValue(item, Convert.ChangeType(reader[property.Name], convertTo), null);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                         Debug.Write("EXECUTE Catch:______________ " + ex.ToString());
                    }

                results.Add(item);
            }

            return results;
        }
    }
}