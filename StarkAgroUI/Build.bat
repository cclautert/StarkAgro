aws ecr get-login-password --region sa-east-1 | docker login --username AWS --password-stdin 199003954469.dkr.ecr.sa-east-1.amazonaws.com
docker build -t starkagro-ui .
docker tag starkagro-ui:latest 199003954469.dkr.ecr.sa-east-1.amazonaws.com/starkagro-ui:latest
docker push 199003954469.dkr.ecr.sa-east-1.amazonaws.com/starkagro-ui:latest
