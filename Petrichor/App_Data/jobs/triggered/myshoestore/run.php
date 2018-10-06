<?php
header('Content-type: text/html; charset=UTF-8') ;
$servername = 'db63.grserver.gr';
$dbuser = 'vraveeodotcom';
$password = 'fiverr.123';
$dbname = 'vraveeo';

// Create connection
$conn = new mysqli($servername, $dbuser, $password, $dbname,3306);
// Check connection
if ($conn->connect_error) {
	die("Connection failed: " . $conn->connect_error);
}
// Change character set to utf8
$conn->set_charset("utf8");
date_default_timezone_set('Europe/Athens');

$date_modified = strtotime("now");
$status = 1;
$date = date('d-m-Y H:i:s');
$business_id = 68; //myshoestore
$xml_link = "https://www.myshoestore.gr/?skroutz=DhITQhuALMMtUj7cnsD6pXvo";
$xml_link = $conn->real_escape_string($xml_link);
$xml_link = trim(stripslashes($xml_link));
		
//UPDATE modified date
$update_business_xml = $conn->query('UPDATE business_xml SET date_modified="' . $date_modified . '" WHERE business_id=68');		

// CLEAN OUT TEMP TABLE
$sql = 'DELETE FROM products_temp';
$delete_xml = $conn->query($sql);

// PREPARED STATEMENT
$sql = 'INSERT INTO products_temp (`business_id`, `pid`, `name`, `category`, `product_link`, `price`,
                                   `size`, `color`, `weight`, `description`, `manufacturer`, `mpn`, `ean`,
                                   `image`, `sku`, `instock`, `availability`, `status`, `date_added`) 
        VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)';

// APPEND ALL RAW XML DATA INTO TEMP TABLE (IN LOOP, WITHOUT INNER IF LOGIC)
//...same xml objects

$reader = new XMLReader();
$reader->open($xml_link);

while($reader->read()) {
    if($reader->nodeType == XMLReader::ELEMENT && $reader->name == 'product') {
		
        $product = new SimpleXMLElement($reader->readOuterXml());
		
		$pid = $product->id;
		$pid = mysqli_real_escape_string($conn,stripslashes($pid)); //may be string
        $name = $product->name;
		$name = mysqli_real_escape_string($conn,stripslashes(trim(mb_strtolower($name,'UTF-8'))));
        $mpn = $product->mpn;
        $mpn = mysqli_real_escape_string($conn,stripslashes(trim(mb_strtolower($mpn,'UTF-8'))));
        $ean = $product->ean;
        $ean = mysqli_real_escape_string($conn,stripslashes(trim(mb_strtolower($ean,'UTF-8'))));
        $sku = $product->sku;
        $sku = mysqli_real_escape_string($conn,stripslashes(trim(mb_strtolower($sku,'UTF-8'))));
        $link = $product->link;
		$check_product_url = $link;
        $price = $product->price_with_vat;
        $price = mysqli_real_escape_string($conn,stripslashes(trim($price)));
        //$price = str_replace(".",",",$price);
        //$category_id =  $product->category->attributes();
        //$category_id =  $product->category_id;
        $category_path = $product->category;
		$category_path = mysqli_real_escape_string($conn,stripslashes(trim(mb_strtolower($category_path,'UTF-8'))));
        $image = $product->image;
		$product_image = $image;
        $availability = $product->availability;
        $availability = mysqli_real_escape_string($conn,stripslashes(trim($availability)));
        $size = $product->size;
        $size = mysqli_real_escape_string($conn,stripslashes(trim(mb_strtolower($size,'UTF-8'))));
        $color = $product->color;
        $color = mysqli_real_escape_string($conn,stripslashes(trim(mb_strtolower($color,'UTF-8'))));
        $weight = $product->weight;
        $weight = mysqli_real_escape_string($conn,stripslashes(trim($weight)));
        $description = $product->description;
        $description = mysqli_real_escape_string($conn,stripslashes(trim(mb_strtolower($description,'UTF-8'))));
        $manufacturer = $product->manufacturer;
        $manufacturer = mysqli_real_escape_string($conn,stripslashes(trim(mb_strtolower(trim($manufacturer,'UTF-8')))));
        $instock = "Y";
        
        $stmt = $conn->prepare($sql);

        $stmt->bind_param("issssssssssssssssis", 
			$business_id, 
			$pid,
			$name, 
			$category_path, 
			$check_product_url, 
			$price, 
			$size, 
			$color, 
			$weight, 
			$description, 
			$manufacturer, 
			$mpn, 
			$ean, 
			$product_image, 
			$sku, 
			$instock, 
			$availability, 
			$status, 
			$date);

        $stmt->execute();
    }
}

$reader->close();

// APPEND ONLY NEW TEMP PRODUCTS WITH RELEVANT INFO AND NOT IN SPECIAL CATEGS INTO PRODUCTS
$sql = 'INSERT INTO products (`business_id`, `pid`, `name`, `category`, `product_link`, `price`,
                              `size`, `color`, `weight`, `description`, `manufacturer`, `mpn`, `ean`,
                              `image`, `sku`, `instock`, `availability`, `status`, `date_added`) 
        SELECT t.business_id, t.pid, t.name, t.category, t.product_link, t.price,
               t.size, t.color, t.weight, t.description, t.manufacturer, t.mpn, t.ean,
               t.image, t.sku, t.instock, t.availability, t.status, t.date_added
        FROM products_temp t
        WHERE NOT EXISTS (SELECT 1 FROM products sub 
                          WHERE sub.pid = t.pid AND sub.business_id = t.business_id) 
          AND t.image IS NOT NULL AND t.price IS NOT NULL AND t.name IS NOT NULL 
          AND t.product_link IS NOT NULL AND t.manufacturer IS NOT NULL';   //AND t.category_id NOT IN (604, 613, 635)
																			//if INSERT does not work, means that some of the values are EMPTY
$insert_business_xml = $conn->query($sql);

// UPDATE MATCHED TEMP PRODUCTS WITH MISSING RELEVANT INFO OR IN SPECIAL CATEGS (I.E., ERRORS)
$sql = "UPDATE products p INNER JOIN products_temp t
        ON p.pid = t.pid AND p.business_id = t.business_id 
        SET p.status=0, p.date_modified = ? 
        WHERE t.image='' OR t.price='' OR t.name='' 
           OR t.product_link='' OR t.manufacturer='' "; //OR t.category_id IN (604, 613, 635)
		   
// if($query = $conn->prepare($sql)) { // assuming $mysqli is the connection
    // $query->bind_param('s', $date_modified);
    // $query->execute();
    // // any additional code you need would go here.
// } else {
    // $error = $conn->errno . ' ' . $conn->error;
    // echo $error; // 1054 Unknown column 'foo' in 'field list'
// }
$stmt = $conn->prepare($sql);
$stmt->bind_param("s", $date_modified);
$stmt->execute();
//$count_errors = $mysqli->affected_rows;     // ERRORS FOR MESSAGE AT END


// UPDATE EXISTING MATCHED TEMP PRODUCTS WITH RELEVANT INFO AND NOT IN SPECIAL CATEGS
$sql = 'UPDATE products p INNER JOIN products_temp t
                           ON p.pid = t.pid AND p.business_id = t.business_id
        SET p.business_id = t.business_id, p.name = t.name, p.category = t.category, 
            p.product_link = t.product_link, p.price = t.price, p.size = t.size, 
            p.color = t.color, p.weight = t.weight, p.description = t.description, 
            p.manufacturer = t.manufacturer, p.mpn = t.mpn, p.ean = t.ean,
            p.image = t.image, p.sku = t.sku, p.instock = t.instock, 
            p.availability = t.availability, p.status = t.status, p.date_added = t.date_added
        WHERE t.image IS NOT NULL AND t.price IS NOT NULL AND t.name IS NOT NULL 
          AND t.product_link IS NOT NULL AND t.manufacturer IS NOT NULL'; //AND t.category_id NOT IN (604, 613, 635)
$update_business_xml = $conn->query($sql);

				
$insert_messages = "Your XML file has been updated successfully!";
echo $insert_messages;
//header('Location:../dashboard.php?panel=product_xml&insert_messages='.$insert_messages);
$conn->close();
?>