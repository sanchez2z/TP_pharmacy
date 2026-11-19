using TP_pharmacy.Data;
using TP_pharmacy.Pages.Catalog;
using TP_pharmacy.Services;

namespace TP_pharmacy.Pages;

public class IndexModel(PharmacyDbContext context, CartService cartService) : CatalogIndexModel(context, cartService);
