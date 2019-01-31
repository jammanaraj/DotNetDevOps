
while ( ! (find /var/log/azure/Microsoft.OSTCExtensions.LinuxDiagnostic/extension.log | xargs grep "Start mdsd"));
do
  sleep 30 
done 

n=0
until [ $n -ge 5 ]
do
    sudo apt-get install -y apt-transport-https && break  # substitute your command here
    n=$[$n+1]
    sleep 15
done

n=0
until [ $n -ge 5 ]
do
    sudo apt-get install -y aspnetcore-runtime-2.2 && break  # substitute your command here
    n=$[$n+1]
    sleep 15
done

n=0
until [ $n -ge 5 ]
do
    sudo apt-get install -y nginx-extras && break  # substitute your command here
    n=$[$n+1]
    sleep 15
done

echo $(hostname) | sudo tee /var/www/html/index.html
sudo mkdir -p /var/www/html/images
echo "Images: " $(hostname) | sudo tee /var/www/html/images/test.html
sudo mkdir -p /var/www/html/video
echo "Video: " $(hostname) | sudo tee /var/www/html/video/test.html

sudo mkdir /mnt/sf_gateway
sudo chmod -R 757 /mnt/sf_gateway/

