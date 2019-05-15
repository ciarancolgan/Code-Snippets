public static class EFExtensions
    {
        public static EntityOp<TEntity> Upsert<TEntity>(this DbContext context, IEnumerable<TEntity> entity, bool identityInsertOn = false) where TEntity : class
        {
            return new UpsertOp<TEntity>(context, entity, identityInsertOn);
        }
    }

    public abstract class EntityOp<TEntity, TRet>
    {
        public readonly DbContext Context;
        public readonly IEnumerable<TEntity> EntityList;
        public readonly string TableName;
        public readonly string EntityPrimaryKeyName;

        private readonly bool _identityInsertOn;
        private readonly List<string> keyNames = new List<string>();
        public IEnumerable<string> KeyNames { get { return keyNames; } }

        private readonly List<string> excludeProperties = new List<string>();

        private static string GetMemberName<T>(Expression<Func<TEntity, T>> selectMemberLambda)
        {
            var member = selectMemberLambda.Body as MemberExpression;
            if (member == null)
            {
                throw new ArgumentException("The parameter selectMemberLambda must be a member accessing labda such as x => x.Id", nameof(selectMemberLambda));
            }
            return member.Member.Name;
        }

        public EntityOp(DbContext context, IEnumerable<TEntity> entityList, bool identityInsertOn = false)
        {
            Context = context;
            EntityList = entityList;
            TableName = GetTableName(typeof(TEntity), context, out EntityPrimaryKeyName);
            _identityInsertOn = identityInsertOn;
        }

        public abstract TRet Execute();
        public void Run()
        {
            Execute();
        }

        public EntityOp<TEntity, TRet> Key<TKey>(Expression<Func<TEntity, TKey>> selectKey)
        {
            keyNames.Add(GetMemberName(selectKey));
            return this;
        }

        public EntityOp<TEntity, TRet> ExcludeField<TField>(Expression<Func<TEntity, TField>> selectField)
        {
            excludeProperties.Add(GetMemberName(selectField));
            return this;
        }

        public IEnumerable<PropertyInfo> ColumnProperties
        {
            get
            {
                // Dont include virtual navigation properties
                var columnProps = typeof(TEntity).GetProperties().Where(pr => !excludeProperties.Contains(pr.Name) && !pr.GetMethod.IsVirtual);

                if (!_identityInsertOn)
                {
                    // If Identity insert is off (default), do not include the PK in the insert.
                    columnProps = columnProps.Where(pr => pr.Name != EntityPrimaryKeyName);
                }

                return columnProps;
            }
        }

        public static string GetTableName(Type type, DbContext context, out string EntityPrimaryKeyName)
        {
            var metadata = ((IObjectContextAdapter)context).ObjectContext.MetadataWorkspace;

            // Get the part of the model that contains info about the actual CLR types
            var objectItemCollection = ((ObjectItemCollection)metadata.GetItemCollection(DataSpace.OSpace));

            // Get the entity type from the model that maps to the CLR type
            var entityType = metadata
                    .GetItems<EntityType>(DataSpace.OSpace)
                    .Single(e => objectItemCollection.GetClrType(e) == type);

            // Get the entity set that uses this entity type
            var entitySet = metadata
                .GetItems<EntityContainer>(DataSpace.CSpace)
                .Single()
                .EntitySets
                .Single(s => s.ElementType.Name == entityType.Name);

            // Find the mapping between conceptual and storage model for this entity set
            var mapping = metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace)
                    .Single()
                    .EntitySetMappings
                    .Single(s => s.EntitySet == entitySet);

            // Get the name of the primary key for the table as we wish to exclude this from the column mapping if the 'identityInsertOn' flag is false.
            EntityPrimaryKeyName = mapping.EntitySet.ElementType.KeyMembers.Select(k => k.Name).FirstOrDefault();

            // Find the storage entity set (table) that the entity is mapped
            var table = mapping
                .EntityTypeMappings.Single()
                .Fragments.Single()
                .StoreEntitySet;

            // Return the table name from the storage entity set
            return (string)table.MetadataProperties["Table"].Value ?? table.Name;
        }
    }

    public abstract class EntityOp<TEntity> : EntityOp<TEntity, int>
    {
        public EntityOp(DbContext context, IEnumerable<TEntity> entityList, bool identityInsertOn = false) : base(context, entityList, identityInsertOn) { }

        public sealed override int Execute()
        {
            ExecuteNoRet();
            return 0;
        }

        protected abstract void ExecuteNoRet();
    }

    public class UpsertOp<TEntity> : EntityOp<TEntity>
    {
        private readonly bool _identityInsertOn;

        public UpsertOp(DbContext context, IEnumerable<TEntity> entityList, bool identityInsertOn = false) : base(context, entityList, identityInsertOn)
        {
            _identityInsertOn = identityInsertOn;
        }

        protected override void ExecuteNoRet()
        {
            var currentCommandTimeout = Context.Database.CommandTimeout;
            try
            {
                // Up the timeout as this may take a while for scheduled batch jobs.
                Context.Database.CommandTimeout = 300;

                StringBuilder sql = new StringBuilder();

                int notNullFields = 0;
                var valueKeyList = new List<string>();
                var columnList = new List<string>();

                var columnProperties = ColumnProperties.ToArray();
                foreach (var p in columnProperties)
                {
                    if (!p.IsDefined(typeof(DoNotIncludeInUpsertAttribute), false))
                    {
                        columnList.Add(p.Name);
                        valueKeyList.Add("{" + (notNullFields++) + "}");
                    }
                }
                var columns = columnList.ToArray();

                sql.Append("merge into ");
                sql.Append(TableName);
                sql.Append(" as T ");

                sql.Append("using (values (");
                sql.Append(string.Join(",", valueKeyList.ToArray()));
                sql.Append(")) as S (");
                sql.Append(string.Join(",", columns.Select(x => $"[{x}]")));
                sql.Append(") ");

                sql.Append("on (");
                var mergeCond = string.Join(" and ", KeyNames.Select(kn => "T.[" + kn + "]=S.[" + kn + "]"));
                sql.Append(mergeCond);
                sql.Append(") ");

                sql.Append("when matched then update set ");

                // If Identity Insert is on, we will have been supplied the PK in the list of columns. Wish to exclude this from the update statement as updating a PK is not allowed.
                var colsToUpdate = _identityInsertOn ? columns.Where(pr => pr != EntityPrimaryKeyName) : columns;

                sql.Append(string.Join(",", colsToUpdate.Select(c => "T.[" + c + "]=S.[" + c + "]").ToArray()));

                sql.Append(" when not matched then insert (");
                sql.Append(string.Join(",", columns.Select(x => $"[{x}]")));
                sql.Append(") values (S.");
                sql.Append(string.Join(",S.", columns.Select(x => $"[{x}]")));
                sql.Append(");");
                var command = sql.ToString();

                foreach (var entity in EntityList)
                {
                    var valueList = new List<object>();

                    foreach (var p in columnProperties)
                    {
                        if (!p.IsDefined(typeof(DoNotIncludeInUpsertAttribute), false))
                        {
                            var val = p.GetValue(entity, null);
                            valueList.Add(val ?? DBNull.Value);
                        }
                    }

                    Context.Database.ExecuteSqlCommand(command, valueList.ToArray());
                }
            }
            finally
            {
                // Reset the timeout to the original value.
                Context.Database.CommandTimeout = currentCommandTimeout;
            }
        }
    }
