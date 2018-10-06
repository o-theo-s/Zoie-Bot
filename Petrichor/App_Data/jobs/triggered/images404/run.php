<?php
header('Content-type: text/html; charset=UTF-8') ;
$servername = 'db63.grserver.gr';
$dbuser = 'vraveeodotcom';
$password = 'fiverr.123';
$dbname = 'vraveeo';
$conn = new mysqli($servername, $dbuser, $password, $dbname,3306);
if ($conn->connect_error) {
	die("Connection failed: " . $conn->connect_error);
}
$conn->set_charset("utf8");
//date_default_timezone_set('Europe/Athens');
$count_false = 0;
$count_true = 0;
$sql = $conn->query("SELECT id, image FROM products WHERE status=1") or die(mysql_error());
$row_sql = $sql->num_rows;
if($row_sql > 0) {
	while($exe = mysqli_fetch_object($sql)) {
		//$page  = $exe->product_link;
		$image = $exe->image;
		$id = $exe->id;
		//$file_page = @get_headers($page);
		$file_image = @get_headers($image);
		if(!$file_image || $file_image[0] == 'HTTP/1.1 404 Not Found' || $file_image[0] == "HTTP/1.1 301 Moved Permanently") {
			$count_false++;
			$sql_update = $conn->query("UPDATE products SET status=0 WHERE id='" . $id . "'") or die(mysql_error());
		}
		else {
			$count_true++;
		}
	}
} //end if

echo "Products not found 404: " . $count_false . "<br />";
echo "Products that are ok: " . $count_true;
?>