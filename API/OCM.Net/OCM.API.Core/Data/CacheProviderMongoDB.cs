﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.GeoJsonObjectModel;
using MongoDB.Driver.Linq;
using Newtonsoft.Json;
using OCM.API.Common;
using OCM.API.Common.Model;
using OCM.API.Common.Model.Extended;

namespace OCM.Core.Data
{
    public class MirrorStatus
    {
        public HttpStatusCode StatusCode { get; set; }

        public string Description { get; set; }

        public long TotalPOIInCache { get; set; }

        public long TotalPOIInDB { get; set; }

        public DateTime LastUpdated { get; set; }

        public long NumPOILastUpdated { get; set; }

        public int NumDistinctPOIs { get; set; }
    }

    public class BenchmarkResult
    {
        public string Description { get; set; }
        public long TimeMS { get; set; }
    }

    public class POIMongoDB : OCM.API.Common.Model.ChargePoint
    {
        [JsonIgnore]
        public GeoJsonPoint<GeoJson2DGeographicCoordinates> SpatialPosition { get; set; }

        public static POIMongoDB FromChargePoint(OCM.API.Common.Model.ChargePoint cp, POIMongoDB poi = null)
        {
            if (poi == null) poi = new POIMongoDB();

            poi.AddressInfo = cp.AddressInfo;
            poi.Chargers = cp.Chargers;
            poi.Connections = cp.Connections;
            poi.DataProvider = cp.DataProvider;
            poi.DataProviderID = cp.DataProviderID;
            poi.DataProvidersReference = cp.DataProvidersReference;
            poi.DataQualityLevel = cp.DataQualityLevel;

            if (cp.DateCreated != null)
            {
                poi.DateCreated = (DateTime?)DateTime.SpecifyKind(cp.DateCreated.Value, DateTimeKind.Utc);
            }
            if (cp.DateLastConfirmed != null)
            {
                poi.DateLastConfirmed = (DateTime?)DateTime.SpecifyKind(cp.DateLastConfirmed.Value, DateTimeKind.Utc);
            }
            if (cp.DateLastStatusUpdate != null)
            {
                poi.DateLastStatusUpdate = (DateTime?)DateTime.SpecifyKind(cp.DateLastStatusUpdate.Value, DateTimeKind.Utc);
            }
            if (cp.DatePlanned != null)
            {
                poi.DatePlanned = (DateTime?)DateTime.SpecifyKind(cp.DatePlanned.Value, DateTimeKind.Utc);
            }

            poi.GeneralComments = cp.GeneralComments;
            poi.ID = cp.ID;
            poi.MediaItems = cp.MediaItems;
            poi.MetadataTags = cp.MetadataTags;
            poi.MetadataValues = cp.MetadataValues;
            poi.NumberOfPoints = cp.NumberOfPoints;
            poi.NumberOfPoints = cp.NumberOfPoints;
            poi.OperatorID = cp.OperatorID;
            poi.OperatorInfo = cp.OperatorInfo;
            poi.OperatorsReference = cp.OperatorsReference;
            poi.ParentChargePointID = cp.ParentChargePointID;
            poi.StatusType = cp.StatusType;
            poi.StatusTypeID = cp.StatusTypeID;
            poi.SubmissionStatus = cp.SubmissionStatus;
            poi.SubmissionStatusTypeID = cp.SubmissionStatusTypeID;
            poi.UsageCost = cp.UsageCost;
            poi.UsageType = cp.UsageType;
            poi.UsageTypeID = cp.UsageTypeID;
            poi.UserComments = cp.UserComments;
            poi.LevelOfDetail = cp.LevelOfDetail;
            poi.UUID = cp.UUID;
            return poi;
        }
    }

    public class CacheProviderMongoDB
    {
        private MongoDatabase database = null;
        private MongoClient client = null;
        private MongoServer server = null;
        private MirrorStatus status = null;

        public CacheProviderMongoDB()
        {
            try
            {
                if (!BsonClassMap.IsClassMapRegistered(typeof(POIMongoDB)))
                {
                    // register deserialization class map for ChargePoint
                    BsonClassMap.RegisterClassMap<POIMongoDB>(cm =>
                    {
                        cm.AutoMap();
                        cm.SetIgnoreExtraElements(true);
                    });
                }

                if (!BsonClassMap.IsClassMapRegistered(typeof(OCM.API.Common.Model.ChargePoint)))
                {
                    // register deserialization class map for ChargePoint
                    BsonClassMap.RegisterClassMap<OCM.API.Common.Model.ChargePoint>(cm =>
                    {
                        cm.AutoMap();
                        cm.SetIgnoreExtraElements(true);
                    });
                }
                if (!BsonClassMap.IsClassMapRegistered(typeof(OCM.API.Common.Model.CoreReferenceData)))
                {
                    // register deserialization class map for ChargePoint
                    BsonClassMap.RegisterClassMap<OCM.API.Common.Model.CoreReferenceData>(cm =>
                    {
                        cm.AutoMap();
                        cm.SetIgnoreExtraElements(true);
                    });
                }

                if (!BsonClassMap.IsClassMapRegistered(typeof(MirrorStatus)))
                {
                    // register deserialization class map for ChargePoint
                    BsonClassMap.RegisterClassMap<MirrorStatus>(cm =>
                    {
                        cm.AutoMap();
                        cm.SetIgnoreExtraElements(true);
                    });
                }

                if (!BsonClassMap.IsClassMapRegistered(typeof(CountryExtendedInfo)))
                {
                    // register deserialization class map for ChargePoint
                    BsonClassMap.RegisterClassMap<CountryExtendedInfo>(cm =>
                    {
                        cm.AutoMap();
                        cm.SetIgnoreExtraElements(true);
                    });
                }
            }
            catch (Exception)
            {
                ; ;
            }

            var connectionString = ConfigurationManager.AppSettings["MongoDB_ConnectionString"].ToString();
            client = new MongoClient(connectionString);
            server = client.GetServer();
            database = server.GetDatabase(ConfigurationManager.AppSettings["MongoDB_Database"]);
            /*if (BsonSerializer.LookupSerializer(typeof(DateTime)) == null)
            {
                BsonSerializer.RegisterSerializer(typeof(DateTime),
                 new DateTimeSerializer(DateTimeSerializationOptions.LocalInstance));
            }*/
            status = GetMirrorStatus(false, false);
        }

        public MongoCollection<POIMongoDB> GetPOICollection()
        {
            return database.GetCollection<POIMongoDB>("poi");
        }

        public void RemoveAllPOI(List<OCM.API.Common.Model.ChargePoint> poiList, MongoCollection<POIMongoDB> poiCollection)
        {
            foreach (var poi in poiList)
            {
                var query = Query.EQ("ID", poi.ID);
                poiCollection.Remove(query);
            }
        }

        public void InsertAllPOI(List<OCM.API.Common.Model.ChargePoint> poiList, MongoCollection<POIMongoDB> poiCollection)
        {
            var mongoDBPoiList = new List<POIMongoDB>();
            foreach (var poi in poiList)
            {
                var newPoi = POIMongoDB.FromChargePoint(poi);
                if (newPoi.AddressInfo != null)
                {
                    newPoi.SpatialPosition = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(new GeoJson2DGeographicCoordinates(newPoi.AddressInfo.Longitude, newPoi.AddressInfo.Latitude));
                }
                mongoDBPoiList.Add(newPoi);
            }
            poiCollection.InsertBatch(mongoDBPoiList);
        }

        public List<OCM.API.Common.Model.ChargePoint> GetPOIListToUpdate(OCMEntities dataModel, CacheUpdateStrategy updateStrategy)
        {
            if (!database.CollectionExists("poi"))
            {
                database.CreateCollection("poi");
            }
            var poiCollection = database.GetCollection<POIMongoDB>("poi");

            //incremental update based on POI Id - up to 100 results at a time
            if (updateStrategy == CacheUpdateStrategy.Incremental)
            {
                var maxPOI = poiCollection.FindAll().SetSortOrder(SortBy.Descending("ID")).SetLimit(1).FirstOrDefault();
                int maxId = 0;
                if (maxPOI != null) { maxId = maxPOI.ID; }

                //from max poi we have in mirror to next 100 results in order of ID
                var dataList = dataModel.ChargePoints.Include("AddressInfo,ConnectionInfo,MetadataValue,UserComment,MediaItem,User").OrderBy(o => o.ID).Where(o => o.ID > maxId).Take(100);
                var poiList = new List<OCM.API.Common.Model.ChargePoint>();
                foreach (var cp in dataList)
                {
                    poiList.Add(OCM.API.Common.Model.Extensions.ChargePoint.FromDataModel(cp, true, true, true, true));
                }
                return poiList;
            }

            //update based on POI last modified since last status update
            if (updateStrategy == CacheUpdateStrategy.Modified)
            {
                var maxPOI = poiCollection.FindAll().SetSortOrder(SortBy.Descending("DateLastStatusUpdate")).SetLimit(1).FirstOrDefault();
                DateTime? dateLastModified = null;
                if (maxPOI != null) { dateLastModified = maxPOI.DateLastStatusUpdate.Value.AddMinutes(-10); }

                //determine POI updated since last status update we have in cache
                //db.poi.find().sort({DateLastStatusUpdate:-1})

                //"AddressInfo,ConnectionInfo,MetadataValue,UserComment,MediaItem,User"
                var dataList = new Data.OCMEntities().ChargePoints
                    .Include(a1 => a1.AddressInfo)
                    .Include(a1 => a1.Connections)
                    .Include(a1 => a1.MetadataValues)
                    .Include(a1 => a1.UserComments)
                    .Include(a1 => a1.MediaItems)
                    .OrderBy(o => o.DateLastStatusUpdate)
                    .Where(o => o.DateLastStatusUpdate > dateLastModified).ToList();
                var poiList = new List<OCM.API.Common.Model.ChargePoint>();
                foreach (var cp in dataList)
                {
                    poiList.Add(OCM.API.Common.Model.Extensions.ChargePoint.FromDataModel(cp, true, true, true, true));
                }
                return poiList;
            }
            if (updateStrategy == CacheUpdateStrategy.All)
            {
                var dataList = new Data.OCMEntities().ChargePoints
                   .Include(a1 => a1.AddressInfo)
                   .Include(a1 => a1.Connections)
                   .Include(a1 => a1.MetadataValues)
                   .Include(a1 => a1.UserComments)
                   .Include(a1 => a1.MediaItems)
                   .OrderBy(o => o.ID)
                   .ToList();

                var poiList = new List<OCM.API.Common.Model.ChargePoint>();
                foreach (var cp in dataList)
                {
                    poiList.Add(OCM.API.Common.Model.Extensions.ChargePoint.FromDataModel(cp, true, true, true, true));
                }
                return poiList;
            }

            return null;
        }

        public async Task<MirrorStatus> RefreshCachedPOI(int poiId)
        {
            var mirrorStatus = await Task.Run<MirrorStatus>(() =>
            {
                var dataModel = new OCMEntities();
                var poiModel = dataModel.ChargePoints.FirstOrDefault(p => p.ID == poiId);

                var poiCollection = database.GetCollection<POIMongoDB>("poi");
                var query = Query.EQ("ID", poiId);
                var removeResult = poiCollection.Remove(query);
                System.Diagnostics.Debug.WriteLine("POIs removed from cache [" + poiId + "]:" + removeResult.DocumentsAffected);

                if (poiModel != null)
                {
                    var cachePOI = POIMongoDB.FromChargePoint(OCM.API.Common.Model.Extensions.ChargePoint.FromDataModel(poiModel));
                    if (cachePOI.AddressInfo != null)
                    {
                        cachePOI.SpatialPosition = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(new GeoJson2DGeographicCoordinates(cachePOI.AddressInfo.Longitude, cachePOI.AddressInfo.Latitude));
                    }

                    poiCollection.Insert<POIMongoDB>(cachePOI);
                }
                else
                {
                    //poi not present in master DB, we've removed it from cache
                }

                long numPOIInMasterDB = dataModel.ChargePoints.LongCount();
                return RefreshMirrorStatus(poiCollection.Count(), 1, numPOIInMasterDB);
            });

            return mirrorStatus;
        }

        /// <summary>
        /// Perform full or partial repopulation of POI Mirror in MongoDB
        /// </summary>
        /// <returns></returns>
        public async Task<MirrorStatus> PopulatePOIMirror(CacheUpdateStrategy updateStrategy)
        {
            var dataModel = new Data.OCMEntities();

            if (!database.CollectionExists("poi"))
            {
                database.CreateCollection("poi");
            }

            if (updateStrategy != CacheUpdateStrategy.All)
            {
                if (!database.CollectionExists("reference"))
                {
                    database.CreateCollection("reference");
                }
                if (!database.CollectionExists("status"))
                {
                    database.CreateCollection("status");
                }
                if (!database.CollectionExists("poi"))
                {
                    database.CreateCollection("poi");
                }
            }

            CoreReferenceData coreRefData = new ReferenceDataManager().GetCoreReferenceData(false);
            if (coreRefData != null)
            {
                database.DropCollection("reference");
                //problems clearing data from collection...

                var reference = database.GetCollection<CoreReferenceData>("reference");
                var query = new QueryDocument();
                reference.Remove(query, RemoveFlags.None);

                Thread.Sleep(300);
                reference.Insert(coreRefData);
            }

            var poiList = GetPOIListToUpdate(dataModel, updateStrategy);
            var poiCollection = database.GetCollection<POIMongoDB>("poi");
            if (poiList != null && poiList.Any())
            {
                if (updateStrategy == CacheUpdateStrategy.All)
                {
                    poiCollection.RemoveAll();
                }

                RemoveAllPOI(poiList, poiCollection);
                Thread.Sleep(300);
                InsertAllPOI(poiList, poiCollection);

                poiCollection.CreateIndex(IndexKeys<POIMongoDB>.GeoSpatialSpherical(x => x.SpatialPosition));
                poiCollection.CreateIndex(IndexKeys<POIMongoDB>.Descending(x => x.DateLastStatusUpdate));
                poiCollection.CreateIndex(IndexKeys<POIMongoDB>.Descending(x => x.DateCreated));

                if (updateStrategy == CacheUpdateStrategy.All)
                {
                    poiCollection.ReIndex();
                }
            }

            long numPOIInMasterDB = dataModel.ChargePoints.LongCount();
            return RefreshMirrorStatus(poiCollection.Count(), 1, numPOIInMasterDB);
        }

        private MirrorStatus RefreshMirrorStatus(long cachePOICollectionCount, long numPOIUpdated, long numPOIInMasterDB)
        {
            var statusCollection = database.GetCollection<MirrorStatus>("status");
            statusCollection.RemoveAll();

            //new status
            MirrorStatus status = new MirrorStatus();
            //status.ID = Guid.NewGuid().ToString();
            status.Description = "MongoDB Cache of Open Charge Map POI Database";
            status.LastUpdated = DateTime.UtcNow;

            status.TotalPOIInCache = cachePOICollectionCount;
            status.TotalPOIInDB = numPOIInMasterDB;
            status.StatusCode = HttpStatusCode.OK;
            status.NumPOILastUpdated = numPOIUpdated;
            status.NumPOILastUpdated = 0;

            statusCollection.Insert(status);
            return status;
        }

        public MirrorStatus GetMirrorStatus(bool includeDupeCheck, bool includeDBPOICount = true)
        {
            try
            {
                var statusCollection = database.GetCollection<MirrorStatus>("status");
                var currentStatus = statusCollection.FindOne();

                if (includeDBPOICount)
                {
                    currentStatus.TotalPOIInDB = new OCMEntities().ChargePoints.LongCount();
                }

                if (includeDupeCheck)
                {
                    //perform check to see if number of distinct ids!= number of pois
                    var poiCollection = database.GetCollection<POIMongoDB>("poi");
                    var distinctPOI = poiCollection.Distinct("ID");
                    currentStatus.NumDistinctPOIs = distinctPOI.Count();

                    /*
                    string dupePOIs = "";
                    foreach (var poi in poiCollection.FindAll())
                    {
                        var itemCount = poiCollection.Count(Query.EQ("ID", poi.ID));
                        if (itemCount > 1)
                        {
                            dupePOIs += " " + poi.ID + " (" + itemCount + "),";
                        }
                    }
                    currentStatus.Description += "Dupe POIs:"+dupePOIs;
                     */
                }
                return currentStatus;
            }
            catch (Exception exp)
            {
                return new MirrorStatus { StatusCode = HttpStatusCode.NotFound, Description = "Cache is offline:" + exp.ToString() };
            }
        }

        public List<CountryExtendedInfo> GetExtendedCountryInfo()
        {
            try
            {
                var results = database.GetCollection<CountryExtendedInfo>("countryinfo");
                return results.FindAll().ToList();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public CoreReferenceData GetCoreReferenceData()
        {
            try
            {
                int maxCacheAgeMinutes = int.Parse(ConfigurationManager.AppSettings["MaxCacheAgeMinutes"]);
                if (status != null && status.LastUpdated.AddMinutes(maxCacheAgeMinutes) > DateTime.UtcNow)
                {
                    var refData = database.GetCollection<CoreReferenceData>("reference");
                    return refData.FindOne();
                }
            }
            catch (Exception)
            {
                ; ;
            }

            return null;
        }

        /// <summary>
        /// returns cached POI details if cache is not older than ageMinutes
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public OCM.API.Common.Model.ChargePoint GetPOI(int id)
        {
            int maxCacheAgeMinutes = int.Parse(ConfigurationManager.AppSettings["MaxCacheAgeMinutes"]);
            if (status != null && status.LastUpdated.AddMinutes(maxCacheAgeMinutes) > DateTime.UtcNow)
            {
                var poiCollection = database.GetCollection<OCM.API.Common.Model.ChargePoint>("poi").AsQueryable();
                return poiCollection.FirstOrDefault(p => p.ID == id);
            }
            else
            {
                return null;
            }
        }

        public List<OCM.API.Common.Model.ChargePoint> GetPOIList(APIRequestParams settings)
        {
            var stopwatch = Stopwatch.StartNew();

            bool freshCache = false;
            int maxCacheAgeMinutes = int.Parse(ConfigurationManager.AppSettings["MaxCacheAgeMinutes"]);
            if (status != null && status.LastUpdated.AddMinutes(maxCacheAgeMinutes) > DateTime.UtcNow)
            {
                freshCache = true;
            }

            if (!freshCache) return null;

            //TODO: share common between POIManager and this
            int maxResults = settings.MaxResults;

            bool requiresDistance = false;
            GeoJsonPoint<GeoJson2DGeographicCoordinates> searchPoint = null;

            if (settings.Latitude != null && settings.Longitude != null)
            {
                requiresDistance = true;

                if (settings.Distance == null) settings.Distance = 100;
                searchPoint = GeoJson.Point(GeoJson.Geographic((double)settings.Longitude, (double)settings.Latitude));
            }
            else
            {
                searchPoint = GeoJson.Point(GeoJson.Geographic(0, 0));
                settings.Distance = 100;
            }

            //if distance filter provided in miles, convert to KM before use

            if (settings.DistanceUnit == OCM.API.Common.Model.DistanceUnit.Miles && settings.Distance != null)
            {
                settings.Distance = GeoManager.ConvertMilesToKM((double)settings.Distance);
            }

            bool filterByConnectionTypes = false;
            bool filterByLevels = false;
            bool filterByOperators = false;
            bool filterByCountries = false;
            bool filterByUsage = false;
            bool filterByStatus = false;
            bool filterByDataProvider = false;
            bool filterByChargePoints = false;

            if (settings.ConnectionTypeIDs != null) { filterByConnectionTypes = true; }
            else { settings.ConnectionTypeIDs = new int[] { -1 }; }

            if (settings.LevelIDs != null) { filterByLevels = true; }
            else { settings.LevelIDs = new int[] { -1 }; }

            if (settings.OperatorIDs != null) { filterByOperators = true; }
            else { settings.OperatorIDs = new int[] { -1 }; }

            if (settings.ChargePointIDs != null) { filterByChargePoints = true; }
            else { settings.ChargePointIDs = new int[] { -1 }; }

            //either filter by named country code or by country id list
            if (settings.CountryCode != null)
            {
                var referenceData = database.GetCollection<OCM.API.Common.Model.CoreReferenceData>("reference").FindOne();

                var filterCountry = referenceData.Countries.FirstOrDefault(c => c.ISOCode.ToUpper() == settings.CountryCode.ToUpper());
                if (filterCountry != null)
                {
                    filterByCountries = true;
                    settings.CountryIDs = new int[] { filterCountry.ID };
                }
                else
                {
                    filterByCountries = false;
                    settings.CountryIDs = new int[] { -1 };
                }
            }
            else
            {
                if (settings.CountryIDs != null && settings.CountryIDs.Any()) { filterByCountries = true; }
                else { settings.CountryIDs = new int[] { -1 }; }
            }

            if (settings.UsageTypeIDs != null) { filterByUsage = true; }
            else { settings.UsageTypeIDs = new int[] { -1 }; }

            if (settings.StatusTypeIDs != null) { filterByStatus = true; }
            else { settings.StatusTypeIDs = new int[] { -1 }; }

            if (settings.DataProviderIDs != null) { filterByDataProvider = true; }
            else { settings.DataProviderIDs = new int[] { -1 }; }

            if (settings.SubmissionStatusTypeID == -1) settings.SubmissionStatusTypeID = null;
            /////////////////////////////////////
            if (database != null)
            {
                var collection = database.GetCollection<OCM.API.Common.Model.ChargePoint>("poi");
                IQueryable<OCM.API.Common.Model.ChargePoint> poiList = from c in collection.AsQueryable<OCM.API.Common.Model.ChargePoint>() select c;

                //filter by points along polyline or bounding box (TODO: polygon)
                if (
                    (settings.Polyline != null && settings.Polyline.Any())
                    || (settings.BoundingBox != null && settings.BoundingBox.Any())
                    || (settings.Polygon != null && settings.Polygon.Any())
                    )
                {
                    //override lat.long specified in search, use polyline or bounding box instead
                    settings.Latitude = null;
                    settings.Longitude = null;

                    double[,] pointList;
                    //filter by locationwithin polylinne expanded to a polygon
                    //TODO; conversion to Km if required
                    IEnumerable<LatLon> searchPolygon = null;

                    if (settings.Polyline != null && settings.Polyline.Any())
                    {
                        searchPolygon = OCM.Core.Util.PolylineEncoder.SearchPolygonFromPolyLine(settings.Polyline, (double)settings.Distance);
                    }

                    if (settings.BoundingBox != null && settings.BoundingBox.Any())
                    {
                        searchPolygon = settings.BoundingBox;
                    }

                    if (settings.Polygon != null && settings.Polygon.Any())
                    {
                        searchPolygon = settings.Polygon;
                    }

                    pointList = new double[searchPolygon.Count(), 2];
                    int pointIndex = 0;
                    foreach (var p in searchPolygon)
                    {
                        pointList[pointIndex, 0] = (double)p.Longitude;
                        pointList[pointIndex, 1] = (double)p.Latitude;
                        pointIndex++;
#if DEBUG
                        System.Diagnostics.Debug.WriteLine(" {lat: " + p.Latitude + ", lng: " + p.Longitude + "},");
#endif
                    }
                    poiList = poiList.Where(q => Query.WithinPolygon("SpatialPosition", pointList).Inject());
                }
                else
                {
                    if (requiresDistance)
                    {
                        //filter by distance from lat/lon first
                        poiList = poiList.Where(q => Query.Near("SpatialPosition", searchPoint, (double)settings.Distance * 1000).Inject());//.Take(settings.MaxResults);
                    }
                }

                poiList = (from c in poiList
                           where

                                       (c.AddressInfo != null) && //c.AddressInfo.Latitude != null && c.AddressInfo.Longitude != null && c.AddressInfo.CountryID != null)
                                       ((settings.SubmissionStatusTypeID == null && (c.SubmissionStatusTypeID == null || c.SubmissionStatusTypeID == (int)StandardSubmissionStatusTypes.Imported_Published || c.SubmissionStatusTypeID == (int)StandardSubmissionStatusTypes.Submitted_Published))
                                             || (settings.SubmissionStatusTypeID == 0) //return all regardless of status
                                             || (settings.SubmissionStatusTypeID != null && c.SubmissionStatusTypeID != null && c.SubmissionStatusTypeID == settings.SubmissionStatusTypeID)
                                             ) //by default return live cps only, otherwise use specific submission statusid
                                       && (c.SubmissionStatusTypeID != null && c.SubmissionStatusTypeID != (int)StandardSubmissionStatusTypes.Delisted_NotPublicInformation)
                                       //&& (settings.ChargePointID == null || (settings.ChargePointID!=null && (c.ID!=null && c.ID == settings.ChargePointID)))
                                       && (settings.OperatorName == null || c.OperatorInfo.Title == settings.OperatorName)
                                       && (settings.IsOpenData == null || (settings.IsOpenData != null && ((settings.IsOpenData == true && c.DataProvider.IsOpenDataLicensed == true) || (settings.IsOpenData == false && c.DataProvider.IsOpenDataLicensed != true))))
                                       && (settings.DataProviderName == null || c.DataProvider.Title == settings.DataProviderName)
                                       && (filterByCountries == false || (filterByCountries == true && settings.CountryIDs.Contains((int)c.AddressInfo.CountryID)))
                                       && (filterByOperators == false || (filterByOperators == true && settings.OperatorIDs.Contains((int)c.OperatorID)))
                                       && (filterByChargePoints == false || (filterByChargePoints == true && settings.ChargePointIDs.Contains((int)c.ID)))
                                       && (filterByUsage == false || (filterByUsage == true && settings.UsageTypeIDs.Contains((int)c.UsageTypeID)))
                                       && (filterByStatus == false || (filterByStatus == true && settings.StatusTypeIDs.Contains((int)c.StatusTypeID)))
                                       && (filterByDataProvider == false || (filterByDataProvider == true && settings.DataProviderIDs.Contains((int)c.DataProviderID)))
                           select c);

                if (settings.ChangesFromDate != null)
                {
                    poiList = poiList.Where(c => c.DateLastStatusUpdate >= settings.ChangesFromDate.Value);
                }

                if (settings.CreatedFromDate != null)
                {
                    poiList = poiList.Where(c => c.DateCreated >= settings.CreatedFromDate.Value);
                }

                //if (settings.LevelOfDetail != null && settings.LevelOfDetail > 1)
                //{
                //    poiList = poiList.Where(q => Query.Mod("ID", (int)settings.LevelOfDetail, 0).Inject());
                //}

                //where level of detail is greater than 1 we decide how much to return based on the given level of detail (1-10) Level 10 will return the least amount of data and is suitable for a global overview
                if (settings.LevelOfDetail > 1)
                {
                    //return progressively less matching results (across whole data set) as requested Level Of Detail gets higher

                    if (settings.LevelOfDetail > 3)
                    {
                        settings.LevelOfDetail = 1; //highest priority LOD
                    }
                    else
                    {
                        settings.LevelOfDetail = 2; //include next level priority items
                    }
                    poiList = poiList.Where(c => c.LevelOfDetail <= settings.LevelOfDetail);
                }

                //apply connectionInfo filters, all filters must match a distinct connection within the charge point, rather than any filter matching any connectioninfo
                if (settings.ConnectionType != null || settings.MinPowerKW != null || filterByConnectionTypes || filterByLevels)
                {
                    poiList = from c in poiList
                              where
                              c.Connections.Any(conn =>
                                    (settings.ConnectionType == null || (settings.ConnectionType != null && conn.ConnectionType.Title == settings.ConnectionType))
                                    && (settings.MinPowerKW == null || (settings.MinPowerKW != null && conn.PowerKW >= settings.MinPowerKW))
                                    && (filterByConnectionTypes == false || (filterByConnectionTypes == true && settings.ConnectionTypeIDs.Contains(conn.ConnectionType.ID)))
                                    && (filterByLevels == false || (filterByLevels == true && settings.LevelIDs.Contains((int)conn.Level.ID)))
                                     )
                              select c;
                }

                List<API.Common.Model.ChargePoint> results = null;
                if (!requiresDistance || (settings.Latitude == null || settings.Longitude == null))
                {
                    //distance is not required or can't be provided
                    results = poiList.OrderByDescending(p => p.DateCreated).Take(settings.MaxResults).ToList();
                }
                else
                {
                    //distance is required, calculate and populate in results
                    results = poiList.ToList();
                    //populate distance
                    foreach (var p in results)
                    {
                        p.AddressInfo.Distance = GeoManager.CalcDistance((double)settings.Latitude, (double)settings.Longitude, p.AddressInfo.Latitude, p.AddressInfo.Longitude, settings.DistanceUnit);
                        p.AddressInfo.DistanceUnit = settings.DistanceUnit;
                    }
                    results = results.OrderBy(r => r.AddressInfo.Distance).Take(settings.MaxResults).ToList();
                }

                if (settings.IsCompactOutput)
                {
                    //dehydrate POI object by removing navigation properties which are based on reference data. Client can then rehydrate using reference data, saving on data transfer KB
                    foreach (var p in results)
                    {
                        //need to null reference data objects so they are not included in output. caution required here to ensure compact output via SQL is same as Cache DB output
                        p.DataProvider = null;
                        p.OperatorInfo = null;
                        p.UsageType = null;
                        p.StatusType = null;
                        p.SubmissionStatus = null;
                        p.AddressInfo.Country = null;
                        if (p.Connections != null)
                        {
                            foreach (var c in p.Connections)
                            {
                                c.ConnectionType = null;
                                c.CurrentType = null;
                                c.Level = null;
                                c.StatusType = null;
                            }
                        }
                        if (p.UserComments != null)
                        {
                            foreach (var c in p.UserComments)
                            {
                                c.CheckinStatusType = null;
                                c.CommentType = null;
                            }
                        }

                        /* if (p.MediaItems != null)
                         {
                             foreach (var c in p.MediaItems)
                             {
                                 c.User = null;
                             }
                         }*/
                    }
                }

                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine("Cache Provider POI Query Time:" + stopwatch.ElapsedMilliseconds + "ms");

                return results;
            }
            else
            {
                return null;
            }
        }

        public List<BenchmarkResult> PerformPOIQueryBenchmark(int numQueries, string mode = "country")
        {
            List<BenchmarkResult> results = new List<BenchmarkResult>();

            for (int i = 0; i < numQueries; i++)
            {
                BenchmarkResult result = new BenchmarkResult();

                try
                {
                    result.Description = "Cached POI Query " + i;

                    APIRequestParams filter = new APIRequestParams();
                    filter.MaxResults = 100;

                    if (mode == "country")
                    {
                        filter.CountryCode = "NL";
                    }
                    else
                    {
                        filter.Latitude = 57.10604;
                        filter.Longitude = -2.62214;
                        filter.Distance = 50;

                        filter.DistanceUnit = DistanceUnit.Miles;
                    }

                    var stopwatch = new Stopwatch();
                    stopwatch.Start();
                    var poiList = this.GetPOIList(filter);
                    stopwatch.Stop();
                    result.Description += " results:" + poiList.Count;
                    result.TimeMS = stopwatch.ElapsedMilliseconds;
                }
                catch (Exception exp)
                {
                    result.Description += " Failed:" + exp.ToString();
                }

                results.Add(result);
            }
            return results;
        }
    }
}