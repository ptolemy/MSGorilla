﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MSGorilla.Library.DAL;
using MSGorilla.Library.Models.SqlModels;
using MSGorilla.Library.Exceptions;

namespace MSGorilla.Library
{
    public class SchemaManager
    {
        private MSGorillaContext _gorillaCtx;

        public SchemaManager()
        {
            _gorillaCtx = new MSGorillaContext();
            Schema defaultSchema = _gorillaCtx.Schemas.Find("none");
            if (defaultSchema == null)
            {
                defaultSchema = new Schema("none", "");
                _gorillaCtx.Schemas.Add(defaultSchema);
                _gorillaCtx.SaveChanges();
            }
        }

        public bool Contain(string schemaID)
        {
            return !(_gorillaCtx.Schemas.Find(schemaID) == null);
        }

        public List<Schema> GetSchema()
        {
            return _gorillaCtx.Schemas.ToList();
        }

        public Schema GetSchema(string schemaID)
        {
            Schema schema = _gorillaCtx.Schemas.Find(schemaID);
            if (schema == null)
            {
                throw new SchemaNotFoundException();
            }

            return schema;
        }

        public void PostSchema(Schema schema)
        {
            if (Contain(schema.SchemaID))
            {
                throw new SchemaAlreadyExistException();
            }
            _gorillaCtx.Schemas.Add(schema);
            _gorillaCtx.SaveChanges();
        }
    }
}
