namespace SharedServices.Identity
{
    public static class AllPermissions
    {
        // A plain permission goes through approval, the .direct one skips it
        public const string RouteView = "route.view";
        public const string RouteCreate = "route.create";
        public const string RouteCreateDirect = "route.create.direct";
        public const string RouteUpdate = "route.update";
        public const string RouteUpdateDirect = "route.update.direct";
        public const string RouteDelete = "route.delete";
        public const string RouteDeleteDirect = "route.delete.direct";
        public const string RouteComplete = "route.complete";

        public const string ProductView = "product.view";
        public const string ProductCreate = "product.create";
        public const string ProductCreateDirect = "product.create.direct";
        public const string ProductUpdate = "product.update";
        public const string ProductUpdateDirect = "product.update.direct";
        public const string ProductDelete = "product.delete";
        public const string ProductDeleteDirect = "product.delete.direct";
    }
}