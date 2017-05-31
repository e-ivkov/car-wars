import random
import socket
from collections import deque
from struct import pack, unpack
import numpy as np


class EnvironmentInterface:

    REQUEST_READ_SENSORS = 1
    REQUEST_WRITE_ACTION = 2
    REQUEST_RESTART = 3

    def __init__(self, host: str, port: int):
        self.socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.socket.connect((host, port))

    @staticmethod
    def _calc_reward(disqualified: bool, finished: bool, velocity: float) -> float:
        if disqualified:
            return 0
        return velocity

    def read_sensors(self):
        request = pack('!b', self.REQUEST_READ_SENSORS)
        self.socket.sendall(request)

        # Response size: reward, velocity, p_X, p_Y, r_Z, sens_1, sens_2, sens_3
        response_size = 8 * 4
        response_buffer = bytes()
        while len(response_buffer) < response_size:
            response_buffer += self.socket.recv(response_size - len(response_buffer))

        response = unpack('!iiiiiiii', response_buffer)
        # Velocity is encoded as x * 2^16
        response = [r/0xffff for r in response]
        reward = response[1]
        state = np.array([response[1:]])
        return reward, state

    '''
    0 - do nothing
    1 - turn 30
    2 - turn -30
    3 - accelerate 10
    4 - decelerate 10
    '''
    def write_action(self, action):
        request = pack('!bi', self.REQUEST_WRITE_ACTION, action)
        self.socket.sendall(request)