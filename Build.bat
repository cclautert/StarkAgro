aws ecr get-login-password --region us-east-2 | docker login --username AWS --password-stdin 199003954469.dkr.ecr.us-east-2.amazonaws.com
docker build -t mcr.microsoft.com/mssql/server:2022-latest .
docker tag mcr.microsoft.com/mssql/server:2022-latest:latest 199003954469.dkr.ecr.us-east-2.amazonaws.com/starkagro-db:latest
docker push 199003954469.dkr.ecr.us-east-2.amazonaws.com/mcr.microsoft.com/mssql/server:2022-latest:latest