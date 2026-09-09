CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE TABLE `branches` (
        `id` char(36) NOT NULL,
        `name` varchar(150) NOT NULL,
        `address` varchar(300) NOT NULL,
        `phone` varchar(20) NULL,
        `latitude` decimal(10,7) NULL,
        `longitude` decimal(10,7) NULL,
        `is_active` tinyint(1) NOT NULL,
        PRIMARY KEY (`id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE TABLE `brands` (
        `id` char(36) NOT NULL,
        `name` varchar(120) NOT NULL,
        `slug` varchar(140) NOT NULL,
        `is_active` tinyint(1) NOT NULL,
        PRIMARY KEY (`id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE TABLE `categories` (
        `id` char(36) NOT NULL,
        `name` varchar(120) NOT NULL,
        `slug` varchar(140) NOT NULL,
        `parent_category_id` char(36) NULL,
        `is_active` tinyint(1) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_categories_categories_parent_category_id` FOREIGN KEY (`parent_category_id`) REFERENCES `categories` (`id`) ON DELETE SET NULL
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE TABLE `products` (
        `id` char(36) NOT NULL,
        `category_id` char(36) NOT NULL,
        `brand_id` char(36) NOT NULL,
        `sku` varchar(64) NOT NULL,
        `name` varchar(200) NOT NULL,
        `slug` varchar(220) NOT NULL,
        `description` text NULL,
        `base_price` decimal(18,2) NOT NULL,
        `unit` varchar(30) NOT NULL,
        `image_url` varchar(500) NULL,
        `is_active` tinyint(1) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_products_brands_brand_id` FOREIGN KEY (`brand_id`) REFERENCES `brands` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_products_categories_category_id` FOREIGN KEY (`category_id`) REFERENCES `categories` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE TABLE `branch_inventories` (
        `id` char(36) NOT NULL,
        `branch_id` char(36) NOT NULL,
        `product_id` char(36) NOT NULL,
        `selling_price` decimal(18,2) NOT NULL,
        `quantity_on_hand` int NOT NULL,
        `reserved_quantity` int NOT NULL,
        `reorder_level` int NOT NULL,
        `updated_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_branch_inventories_branches_branch_id` FOREIGN KEY (`branch_id`) REFERENCES `branches` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_branch_inventories_products_product_id` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE UNIQUE INDEX `IX_branch_inventories_branch_id_product_id` ON `branch_inventories` (`branch_id`, `product_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE INDEX `IX_branch_inventories_product_id` ON `branch_inventories` (`product_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE UNIQUE INDEX `IX_brands_slug` ON `brands` (`slug`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE INDEX `IX_categories_parent_category_id` ON `categories` (`parent_category_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE UNIQUE INDEX `IX_categories_slug` ON `categories` (`slug`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE INDEX `IX_products_brand_id` ON `products` (`brand_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE INDEX `IX_products_category_id` ON `products` (`category_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE UNIQUE INDEX `IX_products_sku` ON `products` (`sku`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    CREATE UNIQUE INDEX `IX_products_slug` ON `products` (`slug`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260807082824_InitialFoundation')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260807082824_InitialFoundation', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260817162519_AddIdentityFoundation')
BEGIN
    CREATE TABLE `users` (
        `id` char(36) NOT NULL,
        `email` varchar(255) NOT NULL,
        `password_hash` varchar(500) NOT NULL,
        `full_name` varchar(150) NOT NULL,
        `phone` varchar(20) NULL,
        `role` varchar(20) NOT NULL,
        `status` varchar(20) NOT NULL,
        `created_at_utc` datetime(6) NOT NULL,
        `updated_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260817162519_AddIdentityFoundation')
BEGIN
    CREATE TABLE `refresh_tokens` (
        `id` char(36) NOT NULL,
        `user_id` char(36) NOT NULL,
        `token_hash` varchar(128) NOT NULL,
        `expires_at_utc` datetime(6) NOT NULL,
        `revoked_at_utc` datetime(6) NULL,
        `replaced_by_token_id` char(36) NULL,
        `created_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_refresh_tokens_refresh_tokens_replaced_by_token_id` FOREIGN KEY (`replaced_by_token_id`) REFERENCES `refresh_tokens` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_refresh_tokens_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260817162519_AddIdentityFoundation')
BEGIN
    CREATE INDEX `IX_refresh_tokens_replaced_by_token_id` ON `refresh_tokens` (`replaced_by_token_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260817162519_AddIdentityFoundation')
BEGIN
    CREATE UNIQUE INDEX `IX_refresh_tokens_token_hash` ON `refresh_tokens` (`token_hash`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260817162519_AddIdentityFoundation')
BEGIN
    CREATE INDEX `IX_refresh_tokens_user_id_expires_at_utc` ON `refresh_tokens` (`user_id`, `expires_at_utc`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260817162519_AddIdentityFoundation')
BEGIN
    CREATE UNIQUE INDEX `IX_users_email` ON `users` (`email`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260817162519_AddIdentityFoundation')
BEGIN
    CREATE INDEX `IX_users_status_role` ON `users` (`status`, `role`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260817162519_AddIdentityFoundation')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260817162519_AddIdentityFoundation', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260821120246_AddAddressesAndPasswordResetTokens')
BEGIN
    CREATE TABLE `addresses` (
        `id` char(36) NOT NULL,
        `user_id` char(36) NOT NULL,
        `recipient_name` varchar(150) NOT NULL,
        `phone` varchar(20) NOT NULL,
        `street` varchar(500) NOT NULL,
        `ward` varchar(100) NOT NULL,
        `district` varchar(100) NOT NULL,
        `city` varchar(100) NOT NULL,
        `postal_code` varchar(20) NULL,
        `is_default` tinyint(1) NOT NULL DEFAULT FALSE,
        `created_at_utc` datetime(6) NOT NULL,
        `updated_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260821120246_AddAddressesAndPasswordResetTokens')
BEGIN
    CREATE TABLE `password_reset_tokens` (
        `id` char(36) NOT NULL,
        `user_id` char(36) NOT NULL,
        `token_hash` varchar(500) NOT NULL,
        `expires_at_utc` datetime(6) NOT NULL,
        `created_at_utc` datetime(6) NOT NULL,
        `is_used` tinyint(1) NOT NULL DEFAULT FALSE,
        PRIMARY KEY (`id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260821120246_AddAddressesAndPasswordResetTokens')
BEGIN
    CREATE INDEX `IX_addresses_user_id` ON `addresses` (`user_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260821120246_AddAddressesAndPasswordResetTokens')
BEGIN
    CREATE INDEX `IX_addresses_user_id_is_default` ON `addresses` (`user_id`, `is_default`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260821120246_AddAddressesAndPasswordResetTokens')
BEGIN
    CREATE INDEX `IX_password_reset_tokens_expires_at_utc` ON `password_reset_tokens` (`expires_at_utc`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260821120246_AddAddressesAndPasswordResetTokens')
BEGIN
    CREATE INDEX `IX_password_reset_tokens_user_id` ON `password_reset_tokens` (`user_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260821120246_AddAddressesAndPasswordResetTokens')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260821120246_AddAddressesAndPasswordResetTokens', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE TABLE `carts` (
        `id` char(36) NOT NULL,
        `user_id` char(36) NOT NULL,
        `branch_id` char(36) NOT NULL,
        `created_at_utc` datetime(6) NOT NULL,
        `updated_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE TABLE `cart_items` (
        `id` char(36) NOT NULL,
        `cart_id` char(36) NOT NULL,
        `product_id` char(36) NOT NULL,
        `branch_inventory_id` char(36) NOT NULL,
        `unit_price` decimal(18,2) NOT NULL,
        `quantity` int NOT NULL,
        `created_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_cart_items_carts_cart_id` FOREIGN KEY (`cart_id`) REFERENCES `carts` (`id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE TABLE `orders` (
        `id` char(36) NOT NULL,
        `user_id` char(36) NOT NULL,
        `branch_id` char(36) NOT NULL,
        `fulfillment_type` varchar(20) NOT NULL,
        `delivery_address_id` char(36) NULL,
        `recipient_name` varchar(100) NOT NULL,
        `recipient_phone` varchar(20) NOT NULL,
        `delivery_address_snapshot` text NULL,
        `subtotal` decimal(18,2) NOT NULL,
        `discount_amount` decimal(18,2) NOT NULL,
        `shipping_fee` decimal(18,2) NOT NULL,
        `total_amount` decimal(18,2) NOT NULL,
        `promotion_id` char(36) NULL,
        `promotion_code_snapshot` varchar(50) NULL,
        `status` varchar(20) NOT NULL,
        `created_at_utc` datetime(6) NOT NULL,
        `updated_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE TABLE `order_items` (
        `id` char(36) NOT NULL,
        `order_id` char(36) NOT NULL,
        `product_id` char(36) NOT NULL,
        `product_name` varchar(200) NOT NULL,
        `sku` varchar(50) NOT NULL,
        `unit_price` decimal(18,2) NOT NULL,
        `quantity` int NOT NULL,
        `line_total` decimal(18,2) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_order_items_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `orders` (`id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE TABLE `order_status_histories` (
        `id` char(36) NOT NULL,
        `order_id` char(36) NOT NULL,
        `from_status` varchar(20) NOT NULL,
        `to_status` varchar(20) NOT NULL,
        `note` varchar(500) NULL,
        `created_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_order_status_histories_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `orders` (`id`) ON DELETE CASCADE
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE TABLE `payments` (
        `id` char(36) NOT NULL,
        `order_id` char(36) NOT NULL,
        `method` varchar(20) NOT NULL,
        `status` varchar(30) NOT NULL,
        `amount` decimal(18,2) NOT NULL,
        `provider_transaction_id` varchar(200) NULL,
        `provider_response` text NULL,
        `created_at_utc` datetime(6) NOT NULL,
        `completed_at_utc` datetime(6) NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_payments_orders_order_id` FOREIGN KEY (`order_id`) REFERENCES `orders` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE TABLE `payment_callbacks` (
        `id` char(36) NOT NULL,
        `payment_id` char(36) NOT NULL,
        `provider` varchar(20) NOT NULL,
        `external_event_id` varchar(200) NOT NULL,
        `raw_response` text NOT NULL,
        `is_valid_signature` tinyint(1) NOT NULL,
        `amount` decimal(18,2) NULL,
        `result_status` varchar(30) NOT NULL,
        `received_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_payment_callbacks_payments_payment_id` FOREIGN KEY (`payment_id`) REFERENCES `payments` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE INDEX `ix_carts_user_id` ON `carts` (`user_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE UNIQUE INDEX `ix_carts_user_branch` ON `carts` (`user_id`, `branch_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE INDEX `ix_cart_items_cart_id` ON `cart_items` (`cart_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE INDEX `ix_orders_user_id` ON `orders` (`user_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE INDEX `ix_orders_status` ON `orders` (`status`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE INDEX `ix_orders_created` ON `orders` (`created_at_utc`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE INDEX `ix_order_items_order_id` ON `order_items` (`order_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE INDEX `ix_order_status_histories_order_id` ON `order_status_histories` (`order_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE INDEX `ix_payments_order_id` ON `payments` (`order_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE INDEX `ix_payments_provider_tx_id` ON `payments` (`provider_transaction_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    CREATE UNIQUE INDEX `ix_payment_callbacks_provider_event` ON `payment_callbacks` (`provider`, `external_event_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260825170000_AddCartsOrdersAndPayments')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260825170000_AddCartsOrdersAndPayments', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260901171024_AddPromotions')
BEGIN
    CREATE TABLE `promotions` (
        `id` char(36) NOT NULL,
        `code` varchar(50) NOT NULL,
        `discount_type` varchar(20) NOT NULL,
        `discount_value` decimal(18,2) NOT NULL,
        `min_order_amount` decimal(18,2) NOT NULL,
        `usage_limit` int NULL,
        `usage_count` int NOT NULL,
        `is_active` tinyint(1) NOT NULL,
        `created_at_utc` datetime(6) NOT NULL,
        `updated_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260901171024_AddPromotions')
BEGIN
    CREATE UNIQUE INDEX `ix_promotions_code` ON `promotions` (`code`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260901171024_AddPromotions')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260901171024_AddPromotions', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903145758_AddInventoryTransactions')
BEGIN
    CREATE TABLE `inventory_transactions` (
        `id` char(36) NOT NULL,
        `branch_inventory_id` char(36) NOT NULL,
        `transaction_type` varchar(30) NOT NULL,
        `quantity_on_hand_delta` int NOT NULL,
        `reserved_quantity_delta` int NOT NULL,
        `quantity_on_hand_after` int NOT NULL,
        `reserved_quantity_after` int NOT NULL,
        `reference_type` varchar(30) NOT NULL,
        `reference_id` char(36) NULL,
        `operation_key` varchar(180) NULL,
        `actor_user_id` char(36) NULL,
        `note` varchar(500) NULL,
        `created_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_inventory_transactions_branch_inventories_branch_inventory_id` FOREIGN KEY (`branch_inventory_id`) REFERENCES `branch_inventories` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_inventory_transactions_users_actor_user_id` FOREIGN KEY (`actor_user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903145758_AddInventoryTransactions')
BEGIN
    CREATE INDEX `IX_inventory_transactions_actor_user_id` ON `inventory_transactions` (`actor_user_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903145758_AddInventoryTransactions')
BEGIN
    CREATE INDEX `ix_inventory_transactions_inventory_created` ON `inventory_transactions` (`branch_inventory_id`, `created_at_utc`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903145758_AddInventoryTransactions')
BEGIN
    CREATE UNIQUE INDEX `ix_inventory_transactions_operation_key` ON `inventory_transactions` (`operation_key`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903145758_AddInventoryTransactions')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903145758_AddInventoryTransactions', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160131_AddReviews')
BEGIN
    CREATE TABLE `reviews` (
        `id` char(36) NOT NULL,
        `user_id` char(36) NOT NULL,
        `order_item_id` char(36) NOT NULL,
        `product_id` char(36) NOT NULL,
        `rating` tinyint NOT NULL,
        `comment` varchar(2000) NULL,
        `created_at_utc` datetime(6) NOT NULL,
        `updated_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `ck_reviews_rating` CHECK (rating >= 1 AND rating <= 5),
        CONSTRAINT `FK_reviews_order_items_order_item_id` FOREIGN KEY (`order_item_id`) REFERENCES `order_items` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_reviews_products_product_id` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_reviews_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160131_AddReviews')
BEGIN
    CREATE UNIQUE INDEX `ix_reviews_order_item_id` ON `reviews` (`order_item_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160131_AddReviews')
BEGIN
    CREATE INDEX `ix_reviews_product_created` ON `reviews` (`product_id`, `created_at_utc`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160131_AddReviews')
BEGIN
    CREATE INDEX `ix_reviews_user_id` ON `reviews` (`user_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903160131_AddReviews')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903160131_AddReviews', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903161025_AddProductViewEvents')
BEGIN
    CREATE TABLE `product_view_events` (
        `id` char(36) NOT NULL,
        `product_id` char(36) NOT NULL,
        `user_id` char(36) NULL,
        `anonymous_session_id` char(36) NULL,
        `branch_id` char(36) NULL,
        `viewed_at_utc` datetime(6) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_product_view_events_branches_branch_id` FOREIGN KEY (`branch_id`) REFERENCES `branches` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_product_view_events_products_product_id` FOREIGN KEY (`product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_product_view_events_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903161025_AddProductViewEvents')
BEGIN
    CREATE INDEX `ix_product_view_events_anonymous_viewed` ON `product_view_events` (`anonymous_session_id`, `viewed_at_utc`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903161025_AddProductViewEvents')
BEGIN
    CREATE INDEX `IX_product_view_events_branch_id` ON `product_view_events` (`branch_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903161025_AddProductViewEvents')
BEGIN
    CREATE INDEX `ix_product_view_events_product_viewed` ON `product_view_events` (`product_id`, `viewed_at_utc`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903161025_AddProductViewEvents')
BEGIN
    CREATE INDEX `ix_product_view_events_user_viewed` ON `product_view_events` (`user_id`, `viewed_at_utc`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903161025_AddProductViewEvents')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903161025_AddProductViewEvents', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903170019_AddBackgroundJobRuns')
BEGIN
    CREATE TABLE `background_job_runs` (
        `id` char(36) NOT NULL,
        `job_name` varchar(100) NOT NULL,
        `lock_key` varchar(100) NOT NULL,
        `status` varchar(20) NOT NULL,
        `created_at_utc` datetime(6) NOT NULL,
        `started_at_utc` datetime(6) NULL,
        `completed_at_utc` datetime(6) NULL,
        `error_summary` varchar(1000) NULL,
        `lock_token` varchar(50) NULL,
        `lease_expires_at_utc` datetime(6) NULL,
        PRIMARY KEY (`id`)
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903170019_AddBackgroundJobRuns')
BEGIN
    CREATE UNIQUE INDEX `IX_background_job_runs_job_name_lock_key` ON `background_job_runs` (`job_name`, `lock_key`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260903170019_AddBackgroundJobRuns')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260903170019_AddBackgroundJobRuns', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904055925_AddRecommendationResults')
BEGIN
    CREATE TABLE `recommendation_results` (
        `id` char(36) NOT NULL,
        `scope` varchar(30) NOT NULL,
        `audience_key` varchar(100) NOT NULL,
        `user_id` char(36) NULL,
        `source_product_id` char(36) NULL,
        `recommended_product_id` char(36) NOT NULL,
        `score` decimal(12,6) NOT NULL,
        `rank` int NOT NULL,
        `reason` varchar(500) NOT NULL,
        `algorithm_version` varchar(50) NOT NULL,
        `generated_at_utc` datetime(6) NOT NULL,
        `expires_at_utc` datetime(6) NOT NULL,
        `job_run_id` char(36) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `FK_recommendation_results_background_job_runs_job_run_id` FOREIGN KEY (`job_run_id`) REFERENCES `background_job_runs` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_recommendation_results_products_recommended_product_id` FOREIGN KEY (`recommended_product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_recommendation_results_products_source_product_id` FOREIGN KEY (`source_product_id`) REFERENCES `products` (`id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_recommendation_results_users_user_id` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904055925_AddRecommendationResults')
BEGIN
    CREATE INDEX `IX_recommendation_results_recommended_product_id` ON `recommendation_results` (`recommended_product_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904055925_AddRecommendationResults')
BEGIN
    CREATE UNIQUE INDEX `ix_recommendation_results_run_audience_product` ON `recommendation_results` (`job_run_id`, `audience_key`, `recommended_product_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904055925_AddRecommendationResults')
BEGIN
    CREATE INDEX `IX_recommendation_results_source_product_id` ON `recommendation_results` (`source_product_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904055925_AddRecommendationResults')
BEGIN
    CREATE INDEX `IX_recommendation_results_user_id` ON `recommendation_results` (`user_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904055925_AddRecommendationResults')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904055925_AddRecommendationResults', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904151359_AddDemandForecasts')
BEGIN
    CREATE TABLE `demand_forecasts` (
        `id` char(36) NOT NULL,
        `branch_inventory_id` char(36) NOT NULL,
        `horizon_days` int NOT NULL,
        `forecast_start_date` date NOT NULL,
        `forecast_end_date` date NOT NULL,
        `predicted_quantity` decimal(18,2) NOT NULL,
        `actual_data_days` int NOT NULL,
        `data_quality` varchar(20) NOT NULL,
        `algorithm_version` varchar(50) NOT NULL,
        `generated_at_utc` datetime(6) NOT NULL,
        `job_run_id` char(36) NOT NULL,
        PRIMARY KEY (`id`),
        CONSTRAINT `ck_demand_forecasts_horizon` CHECK (horizon_days IN (7, 14)),
        CONSTRAINT `ck_demand_forecasts_predicted_quantity` CHECK (predicted_quantity + 0 >= 0),
        CONSTRAINT `ck_demand_forecasts_actual_data_days` CHECK (actual_data_days + 0 >= 0 AND actual_data_days <= 28),
        CONSTRAINT `FK_demand_forecasts_background_job_runs_job_run_id` FOREIGN KEY (`job_run_id`) REFERENCES `background_job_runs` (`Id`) ON DELETE RESTRICT,
        CONSTRAINT `FK_demand_forecasts_branch_inventories_branch_inventory_id` FOREIGN KEY (`branch_inventory_id`) REFERENCES `branch_inventories` (`id`) ON DELETE RESTRICT
    );
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904151359_AddDemandForecasts')
BEGIN
    CREATE INDEX `IX_demand_forecasts_branch_inventory_id` ON `demand_forecasts` (`branch_inventory_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904151359_AddDemandForecasts')
BEGIN
    CREATE UNIQUE INDEX `ix_demand_forecasts_run_inventory_horizon` ON `demand_forecasts` (`job_run_id`, `branch_inventory_id`, `horizon_days`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904151359_AddDemandForecasts')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904151359_AddDemandForecasts', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904160056_AddBackgroundJobRunBranch')
BEGIN
    ALTER TABLE `background_job_runs` ADD `branch_id` char(36) NULL;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904160056_AddBackgroundJobRunBranch')
BEGIN
    CREATE INDEX `IX_background_job_runs_branch_id` ON `background_job_runs` (`branch_id`);
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904160056_AddBackgroundJobRunBranch')
BEGIN
    ALTER TABLE `background_job_runs` ADD CONSTRAINT `FK_background_job_runs_branches_branch_id` FOREIGN KEY (`branch_id`) REFERENCES `branches` (`id`) ON DELETE RESTRICT;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904160056_AddBackgroundJobRunBranch')
BEGIN
    UPDATE background_job_runs SET branch_id = SUBSTRING_INDEX(lock_key, ':', -1) WHERE job_name = 'Forecast' AND lock_key LIKE 'branch:%' AND branch_id IS NULL
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260904160056_AddBackgroundJobRunBranch')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260904160056_AddBackgroundJobRunBranch', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260905081854_SyncModelAndMigrations')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260905081854_SyncModelAndMigrations', '10.0.9');
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260905094527_RestoreRecommendationConstraints')
BEGIN
    DROP PROCEDURE IF EXISTS `_sp_restore_recommendation_constraints`;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260905094527_RestoreRecommendationConstraints')
BEGIN

    CREATE PROCEDURE `_sp_restore_recommendation_constraints`()
    proc_main: BEGIN
        DECLARE v_table_exists INT DEFAULT 0;
        DECLARE v_violating_count INT DEFAULT 0;
        DECLARE v_rank_exists INT DEFAULT 0;
        DECLARE v_rank_enforced VARCHAR(10) DEFAULT '';
        DECLARE v_rank_clause TEXT DEFAULT '';
        DECLARE v_score_exists INT DEFAULT 0;
        DECLARE v_score_enforced VARCHAR(10) DEFAULT '';
        DECLARE v_score_clause TEXT DEFAULT '';

        -- 1. Kiểm tra bảng recommendation_results có tồn tại không
        SELECT COUNT(*) INTO v_table_exists
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'recommendation_results';

        IF v_table_exists = 0 THEN
            LEAVE proc_main;
        END IF;

        -- 2. Xử lý constraint ck_recommendation_results_rank
        SELECT COUNT(*) INTO v_violating_count
        FROM `recommendation_results`
        WHERE `rank` <= 0 OR `rank` IS NULL;

        IF v_violating_count > 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Cannot apply ck_recommendation_results_rank: violating records exist with rank <= 0.';
        END IF;

        SELECT COUNT(*), COALESCE(MAX(ENFORCED), '')
        INTO v_rank_exists, v_rank_enforced
        FROM information_schema.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'recommendation_results'
          AND CONSTRAINT_TYPE = 'CHECK'
          AND CONSTRAINT_NAME = 'ck_recommendation_results_rank';

        IF v_rank_exists = 0 THEN
            ALTER TABLE `recommendation_results`
            ADD CONSTRAINT `ck_recommendation_results_rank` CHECK (`rank` > 0);
        ELSE
            SELECT COALESCE(MAX(CHECK_CLAUSE), '')
            INTO v_rank_clause
            FROM information_schema.CHECK_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE()
              AND CONSTRAINT_NAME = 'ck_recommendation_results_rank';

            IF v_rank_enforced <> 'YES' OR v_rank_clause NOT LIKE '%rank% > 0%' THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'Constraint ck_recommendation_results_rank exists but is invalid or not enforced.';
            END IF;
        END IF;

        -- 3. Xử lý constraint ck_recommendation_results_score
        SELECT COUNT(*) INTO v_violating_count
        FROM `recommendation_results`
        WHERE score < 0 OR score > 1 OR score IS NULL;

        IF v_violating_count > 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Cannot apply ck_recommendation_results_score: violating records exist with score outside [0, 1].';
        END IF;

        SELECT COUNT(*), COALESCE(MAX(ENFORCEd), '')
        INTO v_score_exists, v_score_enforced
        FROM information_schema.TABLE_CONSTRAINTS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'recommendation_results'
          AND CONSTRAINT_TYPE = 'CHECK'
          AND CONSTRAINT_NAME = 'ck_recommendation_results_score';

        IF v_score_exists = 0 THEN
            ALTER TABLE `recommendation_results`
            ADD CONSTRAINT `ck_recommendation_results_score` CHECK (score + 0 >= 0 AND score + 0 <= 1);
        ELSE
            SELECT COALESCE(MAX(CHECK_CLAUSE), '')
            INTO v_score_clause
            FROM information_schema.CHECK_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE()
              AND CONSTRAINT_NAME = 'ck_recommendation_results_score';

            IF v_score_enforced <> 'YES' OR v_score_clause NOT LIKE '%score%>= 0%' OR v_score_clause NOT LIKE '%score%<= 1%' THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'Constraint ck_recommendation_results_score exists but is invalid or not enforced.';
            END IF;
        END IF;

    END;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260905094527_RestoreRecommendationConstraints')
BEGIN
    CALL `_sp_restore_recommendation_constraints`();
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260905094527_RestoreRecommendationConstraints')
BEGIN
    DROP PROCEDURE IF EXISTS `_sp_restore_recommendation_constraints`;
END;

IF NOT EXISTS(SELECT * FROM `__EFMigrationsHistory` WHERE `MigrationId` = '20260905094527_RestoreRecommendationConstraints')
BEGIN
    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260905094527_RestoreRecommendationConstraints', '10.0.9');
END;

COMMIT;

