
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Server.Contracts;
using Server.Data;
using Server.Entities;
using Server.Helper;
using Server.Utilities.Filter;
using Server.Utilities.Pagination;
using Server.Utilities.Response;

namespace Server.Services;

public class ProductService : IProduct
{
    private EFDataContext _context;
    private readonly ApplicationSettings _appSettings;
    private readonly IUriService _uriService;
    public ProductService(EFDataContext context, ApplicationSettings applicationSettings, IUriService uriService)
    {
        _context = context;
        _appSettings = applicationSettings;
        _uriService = uriService;
    }

    public async Task<Response<SubProduct>> AddSubProduct(SubProduct sub)
    {
        try
        {
            // Kiểm tra đầu vào từ client
            if (sub.Product_Id == Guid.Empty)
            {
                return new Response<SubProduct>
                {
                    IsSuccess = false,
                    Message = "Invalid Product_Id.",
                    HttpStatusCode = HttpStatusCode.BadRequest,
                };
            }

            var subProduct = new SubProduct()
            {
                Id = sub.Id,
                Size = sub.Size,
                Price = sub.Price,
                Color = sub.Color,
                Images = sub.Images,
                Qty = sub.Qty,
                Product_Id = sub.Product_Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            await _context.SubProducts.AddAsync(subProduct);
            await _context.SaveChangesAsync();

            return new Response<SubProduct>
            {
                Data = subProduct,
                SubProductId = subProduct.Id,
                IsSuccess = true,
                Message = "SubProduct created successfully.",
                HttpStatusCode = HttpStatusCode.OK,
            };
        }
        catch (Exception ex)
        {
            return new Response<SubProduct>
            {
                IsSuccess = false,
                Message = "Failed to create SubProduct: " + ex.Message,
                HttpStatusCode = HttpStatusCode.BadRequest,
            };
        }
    }


    public async Task<Response<Product>> AddProduct(Product pro)
    {
        // Khai báo transaction
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var product = new Product()
            {
                Title = pro.Title,
                Slug = pro.Slug,
                Supplier = pro.Supplier,
                Content = pro.Content,
                Images = pro.Images,
                Description = pro.Description,
                ExpiryDate = pro.ExpiryDate,
                CreatedAt = DateTime.UtcNow,
            };

            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();

            if (pro.ProductCategories != null && pro.ProductCategories.Any())
            {
                foreach (var category in pro.ProductCategories)
                {
                    var existingCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Id == category.CategoryId);

                    // Kiểm tra danh mục có tồn tại không
                    if (existingCategory == null)
                    {
                        // Nếu danh mục không hợp lệ, rollback transaction
                        await transaction.RollbackAsync();

                        // Xóa sản phẩm đã thêm nếu danh mục không hợp lệ
                        _context.Products.Remove(product);

                        return new Response<Product>
                        {
                            IsSuccess = false,
                            Message = "Category does not exist.",
                            HttpStatusCode = HttpStatusCode.BadRequest,
                        };
                    }

                    var productCategories = new ProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = category.CategoryId
                    };
                    await _context.ProductCategories.AddAsync(productCategories);
                }
            }

            await _context.SaveChangesAsync();

            await transaction.CommitAsync(); // Commit transaction nếu không có lỗi

            return new Response<Product>
            {
                Data = product,
                ProductId = product.Id,
                IsSuccess = true,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "CreateProductSuccess"),
                HttpStatusCode = HttpStatusCode.OK,
            };
        }
        catch (Exception ex)
        {
            // Nếu có lỗi, rollback transaction
            await transaction.RollbackAsync();

            return new Response<Product>
            {
                IsSuccess = false,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "CreateProductFailure") + " " + ex.Message,
                HttpStatusCode = HttpStatusCode.BadRequest,
            };
        }
    }

    public async Task<Response<Product>> Update(Product pro, Guid ProductId)
    {
        try
        {
            if (ProductId <= Guid.Empty)
            {
                return new Response<Product>
                {
                    ProductId = ProductId,
                    IsSuccess = false,
                    Message = _appSettings.GetConfigurationValue("ProductMessages", "ProductNotFound"),
                    HttpStatusCode = HttpStatusCode.BadRequest,
                };
            }

            var product = _context.Products.Where(x => x.Id == ProductId).FirstOrDefault(sp => sp.Id == ProductId);
            if (product == null)
            {
                return new Response<Product>
                {
                    ProductId = ProductId,
                    IsSuccess = false,
                    Message = _appSettings.GetConfigurationValue("ProductMessages", "ProductNotFound"),
                    HttpStatusCode = HttpStatusCode.BadRequest,
                };
            }

            product.Title = pro.Title;
            product.Slug = pro.Slug;
            product.Supplier = pro.Supplier;
            product.Content = pro.Content;
            product.Description = pro.Description;
            product.Images = pro.Images;
            product.ExpiryDate = pro.ExpiryDate;
            product.UpdatedAt = DateTime.UtcNow;

            _context.Products.Update(product);

            var existingCategoryIds = product.ProductCategories.Select(pc => pc.CategoryId).ToList();
            var newCategoryIds = pro.ProductCategories.Select(pc => pc.CategoryId).ToList();

            if (!existingCategoryIds.SequenceEqual(newCategoryIds))
            {
                var existingProductCategories = _context.ProductCategories.Where(pc => pc.ProductId == ProductId).ToList();
                _context.ProductCategories.RemoveRange(existingProductCategories);

                foreach (var categoryId in newCategoryIds)
                {
                    var productCategory = new ProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = categoryId
                    };
                    await _context.ProductCategories.AddAsync(productCategory);
                }
            }

            await _context.SaveChangesAsync();

            return new Response<Product>
            {
                Data = product,
                ProductId = ProductId,
                IsSuccess = true,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "UpdateProductSuccess"),
                HttpStatusCode = HttpStatusCode.OK,
            };
        }
        catch (Exception ex)
        {
            return new Response<Product>
            {
                IsSuccess = false,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "UpdateProductFailure") + " " +
            ex.Message.ToString(),
                HttpStatusCode = HttpStatusCode.BadRequest,
            };
        }
    }

    public async Task<Response<SubProduct>> UpdateSubProduct(SubProduct sub, Guid SubProductId)
    {
        try
        {
            if (SubProductId <= Guid.Empty)
            {
                return new Response<SubProduct>
                {
                    SubProductId = SubProductId,
                    IsSuccess = false,
                    Message = _appSettings.GetConfigurationValue("ProductMessages", "ProductNotFound"),
                    HttpStatusCode = HttpStatusCode.BadRequest,
                };
            }

            var subProduct = await _context.SubProducts.FindAsync(SubProductId);
            if (subProduct == null)
            {
                return new Response<SubProduct>
                {
                    SubProductId = SubProductId,
                    IsSuccess = false,
                    Message = _appSettings.GetConfigurationValue("ProductMessages", "ProductNotFound"),
                    HttpStatusCode = HttpStatusCode.BadRequest,
                };
            }

            subProduct.Size = sub.Size;
            subProduct.Price = sub.Price;
            subProduct.Color = sub.Color;
            subProduct.Qty = sub.Qty;
            subProduct.Images = sub.Images;

            _context.SubProducts.Update(subProduct);

            await _context.SaveChangesAsync();

            return new Response<SubProduct>
            {
                Data = subProduct,
                SubProductId = SubProductId,
                IsSuccess = true,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "UpdateProductSuccess"),
                HttpStatusCode = HttpStatusCode.OK,
            };
        }
        catch (Exception ex)
        {
            return new Response<SubProduct>
            {
                IsSuccess = false,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "UpdateProductFailure") + " " +
            ex.Message.ToString(),
                HttpStatusCode = HttpStatusCode.BadRequest,
            };
        }
    }

    public async Task<Response<Product>> Delete(Guid ProductId)
    {
        try
        {
            if (ProductId <= Guid.Empty)
            {
                return new Response<Product>
                {
                    IsSuccess = false,
                    Message = _appSettings.GetConfigurationValue("ProductMessages", "ProductNotFound"),
                    HttpStatusCode = HttpStatusCode.BadRequest,
                };
            }
            var product = _context.Products.Where(x => x.Id == ProductId).FirstOrDefault();
            if (product == null)
            {
                return new Response<Product>
                {
                    IsSuccess = false,
                    Message = _appSettings.GetConfigurationValue("ProductMessages", "ProductNotFound"),
                    HttpStatusCode = HttpStatusCode.BadRequest,
                };
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return new Response<Product>
            {
                Data = product,
                IsSuccess = true,
                ProductId = ProductId,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "DeleteProductSuccess"),
                HttpStatusCode = HttpStatusCode.OK,
            };
        }
        catch (Exception ex)
        {
            return new Response<Product>
            {
                IsSuccess = false,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "DeleteProductFailure") + " " +
            ex.Message.ToString(),
                HttpStatusCode = HttpStatusCode.BadRequest,
            };
        }
    }

    public async Task<Response<SubProduct>> DeleteSubProduct(Guid SubProductId)
    {
        try
        {
            if (SubProductId <= Guid.Empty)
            {
                return new Response<SubProduct>
                {
                    IsSuccess = false,
                    Message = _appSettings.GetConfigurationValue("ProductMessages", "ProductNotFound"),
                    HttpStatusCode = HttpStatusCode.BadRequest,
                };
            }
            var sub = _context.SubProducts.Where(x => x.Id == SubProductId).FirstOrDefault();
            if (sub == null)
            {
                return new Response<SubProduct>
                {
                    IsSuccess = false,
                    Message = _appSettings.GetConfigurationValue("ProductMessages", "ProductNotFound"),
                    HttpStatusCode = HttpStatusCode.BadRequest,
                };
            }

            _context.SubProducts.Remove(sub);
            await _context.SaveChangesAsync();

            return new Response<SubProduct>
            {
                Data = sub,
                IsSuccess = true,
                SubProductId = SubProductId,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "DeleteProductSuccess"),
                HttpStatusCode = HttpStatusCode.OK,
            };
        }
        catch (Exception ex)
        {
            return new Response<SubProduct>
            {
                IsSuccess = false,
                Message = _appSettings.GetConfigurationValue("ProductMessages", "DeleteProductFailure") + " " +
            ex.Message.ToString(),
                HttpStatusCode = HttpStatusCode.BadRequest,
            };
        }
    }

    public async Task<List<Product>> GetAllProducts()
    {
        try
        {
            var products = await _context.Products
                    .Include(p => p.ProductCategories)
                    .Include(p => p.SubProducts)
                    .Select(pro => new Product
                    {
                        Id = pro.Id,
                        Title = pro.Title,
                        Slug = pro.Slug,
                        Supplier = pro.Supplier,
                        Content = pro.Content,
                        Description = pro.Description,
                        Images = pro.Images,
                        ExpiryDate = pro.ExpiryDate,
                        CreatedAt = pro.CreatedAt,
                        UpdatedAt = pro.UpdatedAt,
                        ProductCategories = pro.ProductCategories.Select(pc => new ProductCategory
                        {
                            CategoryId = pc.CategoryId,
                            Category = new Category
                            {
                                Id = pc.Category.Id,
                                Title = pc.Category.Title,
                            }
                        }).ToList(),
                        SubProducts = pro.SubProducts.Select(sp => new SubProduct
                        {
                            Id = sp.Id,
                            Size = sp.Size,
                            Price = sp.Price,
                            Color = sp.Color,
                            Images = sp.Images,
                            Qty = sp.Qty,
                            Product_Id = sp.Product_Id,
                            CreatedAt = sp.CreatedAt,
                            UpdatedAt = sp.UpdatedAt,
                        }).ToList()
                    })
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();

            return products;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.ToString());
        }
    }

    public async Task<IActionResult> GetPaginationProducts(PaginationFilter filter, string route)
    {
        try
        {
            var validFilter = new PaginationFilter(filter.PageNumber, filter.PageSize);
            var totalRecords = await _context.Products.CountAsync();

            var products = await _context.Products
                    .Include(p => p.ProductCategories)
                    .Include(p => p.SubProducts)
                    .Select(pro => new Product
                    {
                        Id = pro.Id,
                        Title = pro.Title,
                        Slug = pro.Slug,
                        Supplier = pro.Supplier,
                        Content = pro.Content,
                        Description = pro.Description,
                        Images = pro.Images,
                        ExpiryDate = pro.ExpiryDate,
                        CreatedAt = pro.CreatedAt,
                        UpdatedAt = pro.UpdatedAt,
                        ProductCategories = pro.ProductCategories.Select(pc => new ProductCategory
                        {
                            CategoryId = pc.CategoryId,
                            Category = new Category
                            {
                                Id = pc.Category.Id,
                                Title = pc.Category.Title,
                            }
                        }).ToList(),
                        SubProducts = pro.SubProducts.Select(sp => new SubProduct
                        {
                            Id = sp.Id,
                            Size = sp.Size,
                            Price = sp.Price,
                            Color = sp.Color,
                            Images = sp.Images,
                            Qty = sp.Qty,
                            Product_Id = sp.Product_Id,
                            CreatedAt = sp.CreatedAt,
                            UpdatedAt = sp.UpdatedAt,
                        }).ToList()
                    })
                    .OrderByDescending(x => x.Id)
                    .Skip((validFilter.PageNumber - 1) * validFilter.PageSize)
                    .Take(validFilter.PageSize)
                    .ToListAsync();

            var pagedResponse = PaginationHelper.CreatePagedReponse<Product>(products, validFilter, totalRecords, _uriService, route);
            return new OkObjectResult(pagedResponse);
        }
        catch (Exception ex)
        {
            throw new Exception(ex.ToString());
        }
    }


    public async Task<Product> GetById(Guid ProductId)
    {
        try
        {
            var product = await _context.Products
                                .Include(p => p.ProductCategories)
                                .Include(p => p.SubProducts)
                                .Where(c => c.Id == ProductId)
                                .Select(pro => new Product
                                {
                                    Id = pro.Id,
                                    Title = pro.Title,
                                    Slug = pro.Slug,
                                    Supplier = pro.Supplier,
                                    Description = pro.Description,
                                    Content = pro.Content,
                                    Images = pro.Images,
                                    ExpiryDate = pro.ExpiryDate,
                                    CreatedAt = pro.CreatedAt,
                                    UpdatedAt = pro.UpdatedAt,
                                    ProductCategories = pro.ProductCategories.Select(pc => new ProductCategory
                                    {
                                        CategoryId = pc.CategoryId,
                                        Category = new Category
                                        {
                                            Id = pc.Category.Id,
                                            Title = pc.Category.Title,
                                        }
                                    }).ToList(),
                                    SubProducts = pro.SubProducts.Select(sp => new SubProduct
                                    {
                                        Id = sp.Id,
                                        Size = sp.Size,
                                        Price = sp.Price,
                                        Color = sp.Color,
                                        Images = sp.Images,
                                        Qty = sp.Qty,
                                        Product_Id = sp.Product_Id,
                                        CreatedAt = sp.CreatedAt,
                                        UpdatedAt = sp.UpdatedAt,
                                    }).ToList()
                                })
                                .OrderByDescending(x => x.Id).FirstOrDefaultAsync();
            return product;
        }
        catch (Exception ex)
        {
            throw new Exception(ex.ToString());
        }
    }

    public async Task<Response<FilterValues>> GetAllSubProducts()
    {
        try
        {
            // Lấy tất cả SubProducts
            var subProducts = await _context.SubProducts.ToListAsync();

            // Khởi tạo các danh sách để lưu các giá trị
            var colors = new List<string>();
            var sizes = new List<string>();
            var prices = new List<decimal>();

            // Duyệt qua danh sách subProducts và thêm các giá trị vào danh sách
            foreach (var item in subProducts)
            {
                if (!string.IsNullOrEmpty(item.Color) && !colors.Contains(item.Color))
                {
                    colors.Add(item.Color);
                }
                if (!string.IsNullOrEmpty(item.Size) && !sizes.Contains(item.Size))
                {
                    sizes.Add(item.Size);
                }
                prices.Add(item.Price);
            }

            // Trả về kết quả với các giá trị lọc
            return new Response<FilterValues>
            {
                IsSuccess = true,
                Message = "Fetched successfully",
                Data = new FilterValues
                {
                    Colors = colors,
                    Sizes = sizes,
                    Prices = prices
                },
                HttpStatusCode = HttpStatusCode.OK,
            };
        }
        catch (Exception ex)
        {
            return new Response<FilterValues>
            {
                Message = $"Error: {ex.Message}"
            };
        }
    }


    public async Task<List<Product>> FilterProductsAsync(FilterDto filter)
    {

        // Bắt đầu từ bảng SubProduct và áp dụng các bộ lọc
        var query = _context.SubProducts
            .Include(sp => sp.Product)
            .ThenInclude(p => p.SubProducts)
            .Include(sp => sp.Product)
            .ThenInclude(p => p.ProductCategories)
            .ThenInclude(pc => pc.Category)
            .AsQueryable();

        // Lọc theo màu sắc
        if (filter.Colors != null && filter.Colors.Any())
        {
            query = query.Where(sp => filter.Colors.Contains(sp.Color));
        }

        // Lọc theo kích cỡ
        if (!string.IsNullOrEmpty(filter.Size))
        {
            query = query.Where(sp => sp.Size == filter.Size);
        }

        // Lọc theo khoảng giá
        if (filter.Price != null && filter.Price.Length == 2)
        {
            var minPrice = filter.Price[0];
            var maxPrice = filter.Price[1];
            query = query.Where(sp => sp.Price >= minPrice && sp.Price <= maxPrice);
        }

        // Lọc theo danh mục sản phẩm
        if (filter.Categories != null && filter.Categories.Any())
        {
            query = query.Where(sp => sp.Product.ProductCategories
                .Any(pc => filter.Categories.Contains(pc.CategoryId.ToString())));
        }

        // Truy vấn các sản phẩm đã được lọc
        var products = await query.Select(sp => sp.Product).Distinct().ToListAsync();

        return products;
    }


    public async Task<IEnumerable<Product>> Search(string slug)
    {
        IQueryable<Product> query = _context.Products;

        if (!string.IsNullOrEmpty(slug))
        {
            query = query.Where(e => e.Slug.Contains(slug)
                        || e.Title.Contains(slug));
        }

        return await query.ToListAsync();
    }
}