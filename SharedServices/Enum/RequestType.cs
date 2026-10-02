namespace SharedServices.Enum
{
    /// <summary>Request types match their permission names, except product.transfer which route.create gates.</summary>
    public static class RequestType
    {
        public const string CreateProduct = "product.create";
        public const string UpdateProduct = "product.update";
        public const string DeleteProduct = "product.delete";
        public const string TransferProduct = "product.transfer";
        public const string UpdateRoute = "route.update";
        public const string DeleteRoute = "route.delete";
    }
}